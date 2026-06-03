using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Hubs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/chat")]
public class ChatController(AppDbContext dbContext, IHubContext<ChatHub, IChatClient> chatHub) : ControllerBase
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 100;
    private const int MaxContentLength = 500;
    private static readonly HashSet<string> AllowedEmojis = ["👍", "❤️", "😂", "😮", "😢"];

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ChatMessageResponse>>> GetMessages(
        [FromQuery] DateTime? before = null,
        [FromQuery] int limit = DefaultPageSize)
    {
        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        if (limit <= 0 || limit > MaxPageSize) limit = DefaultPageSize;

        var query = dbContext.ChatMessages
            .Where(m => m.BettingGroupId == groupId);

        if (before.HasValue)
        {
            query = query.Where(m => m.CreatedAt < before.Value);
        }

        var userId = GetAuthenticatedUserId()!.Value;

        // Step 1: fetch messages
        var rawMessages = await query
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .Select(m => new
            {
                m.Id,
                m.UserId,
                UserName = m.SenderDisplayNameOverride ?? m.User.Name,
                UserPicture = m.User.Picture,
                Content = m.DeletedAt == null ? m.Content : string.Empty,
                m.CreatedAt,
                IsDeleted = m.DeletedAt != null,
                IsSystem = m.User.IsSystem
            })
            .AsNoTracking()
            .ToListAsync();

        // Step 2: fetch reactions for those messages (avoids GroupBy-in-projection EF translation issues)
        var messageIds = rawMessages.Select(m => m.Id).ToList();
        var reactions = await dbContext.ChatMessageReactions
            .Where(r => messageIds.Contains(r.ChatMessageId))
            .AsNoTracking()
            .ToListAsync();

        var reactionsByMessage = reactions
            .GroupBy(r => r.ChatMessageId)
            .ToDictionary(
                g => g.Key,
                g => g.GroupBy(r => r.Emoji)
                      .Select(eg => new ChatReactionSummary
                      {
                          Emoji = eg.Key,
                          Count = eg.Count(),
                          ReactedByMe = eg.Any(r => r.UserId == userId)
                      })
                      .ToList());

        var messages = rawMessages.Select(m => new ChatMessageResponse
        {
            Id = m.Id,
            UserId = m.UserId,
            UserName = m.UserName,
            UserPicture = m.UserPicture,
            Content = m.Content,
            CreatedAt = m.CreatedAt,
            IsDeleted = m.IsDeleted,
            IsSystem = m.IsSystem,
            Reactions = reactionsByMessage.GetValueOrDefault(m.Id) ?? []
        }).ToList();

        // Return chronological (oldest first) so the client can append
        messages.Reverse();
        return Ok(messages);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<ChatUnreadCountResponse>> GetUnreadCount([FromQuery] DateTime? since = null)
    {
        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        var userId = GetAuthenticatedUserId()!.Value;

        var query = dbContext.ChatMessages
            .Where(m => m.BettingGroupId == groupId
                        && m.DeletedAt == null
                        && m.UserId != userId);

        if (since.HasValue)
        {
            query = query.Where(m => m.CreatedAt > since.Value);
        }

        var count = await query.CountAsync();
        return Ok(new ChatUnreadCountResponse { UnreadCount = count });
    }

    [HttpPost]
    public async Task<ActionResult<ChatMessageResponse>> PostMessage([FromBody] ChatMessageDto request)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        var content = (request.Content ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(content))
            return BadRequest("Meldingen kan ikke være tom.");
        if (content.Length > MaxContentLength)
            return BadRequest($"Meldingen kan maks være {MaxContentLength} tegn.");

        var user = await dbContext.Users.FindAsync(userId.Value);
        if (user is null) return Unauthorized();

        var message = new ChatMessage
        {
            Id = Guid.NewGuid(),
            BettingGroupId = groupId,
            UserId = userId.Value,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        dbContext.ChatMessages.Add(message);
        await dbContext.SaveChangesAsync();

        var response = new ChatMessageResponse
        {
            Id = message.Id,
            UserId = message.UserId,
            UserName = user.Name,
            UserPicture = user.Picture,
            Content = message.Content,
            CreatedAt = message.CreatedAt,
            IsDeleted = false,
            IsSystem = user.IsSystem
        };

        await chatHub.Clients.Group(GroupName(groupId)).MessagePosted(response);

        return StatusCode(StatusCodes.Status201Created, response);
    }

    [HttpPost("{id:guid}/reactions/{emoji}")]
    public async Task<ActionResult> AddReaction(Guid id, string emoji)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        if (!AllowedEmojis.Contains(emoji))
            return BadRequest("Ikke tillatt emoji.");

        var message = await dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == id && m.BettingGroupId == groupId && m.DeletedAt == null);
        if (message is null) return NotFound();

        var existing = await dbContext.ChatMessageReactions
            .FirstOrDefaultAsync(r => r.ChatMessageId == id && r.UserId == userId.Value && r.Emoji == emoji);

        if (existing is null)
        {
            dbContext.ChatMessageReactions.Add(new ChatMessageReaction
            {
                Id = Guid.NewGuid(),
                ChatMessageId = id,
                UserId = userId.Value,
                Emoji = emoji,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        var count = await dbContext.ChatMessageReactions
            .CountAsync(r => r.ChatMessageId == id && r.Emoji == emoji);

        await chatHub.Clients.Group(GroupName(groupId)).ReactionUpdated(new ChatReactionEventDto
        {
            MessageId = id,
            BettingGroupId = groupId,
            Emoji = emoji,
            Count = count
        });

        return NoContent();
    }

    [HttpDelete("{id:guid}/reactions/{emoji}")]
    public async Task<ActionResult> RemoveReaction(Guid id, string emoji)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        var reaction = await dbContext.ChatMessageReactions
            .FirstOrDefaultAsync(r => r.ChatMessageId == id && r.UserId == userId.Value && r.Emoji == emoji);

        if (reaction is not null)
        {
            dbContext.ChatMessageReactions.Remove(reaction);
            await dbContext.SaveChangesAsync();
        }

        var count = await dbContext.ChatMessageReactions
            .CountAsync(r => r.ChatMessageId == id && r.Emoji == emoji);

        await chatHub.Clients.Group(GroupName(groupId)).ReactionUpdated(new ChatReactionEventDto
        {
            MessageId = id,
            BettingGroupId = groupId,
            Emoji = emoji,
            Count = count
        });

        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteMessage(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var (groupId, isValid) = await ValidateGroupMembership();
        if (!isValid) return BadRequest("Ugyldig eller manglende X-Group-Id header.");

        var message = await dbContext.ChatMessages
            .FirstOrDefaultAsync(m => m.Id == id && m.BettingGroupId == groupId);

        if (message is null) return NotFound();
        if (message.DeletedAt != null) return NoContent();

        var canDelete = await CanDeleteMessage(userId.Value, message);
        if (!canDelete) return Forbid();

        message.DeletedAt = DateTime.UtcNow;
        message.DeletedByUserId = userId.Value;
        await dbContext.SaveChangesAsync();

        await chatHub.Clients.Group(GroupName(groupId)).MessageDeleted(new ChatDeletedEventDto
        {
            Id = message.Id,
            BettingGroupId = groupId
        });

        return NoContent();
    }

    private async Task<bool> CanDeleteMessage(Guid userId, ChatMessage message)
    {
        // Systemmeldinger (f.eks. Resultatservice) kan kun slettes av globale admins.
        var author = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == message.UserId);
        if (author?.IsSystem == true)
        {
            return User.IsInRole("Admin");
        }

        // Author can delete their own message
        if (message.UserId == userId) return true;

        // Global admin
        if (User.IsInRole("Admin")) return true;

        // Group admin for the message's group
        return await dbContext.BettingGroupMembers
            .AnyAsync(m => m.BettingGroupId == message.BettingGroupId
                           && m.UserId == userId
                           && m.IsGroupAdmin);
    }

    private static string GroupName(Guid groupId) => $"group-{groupId}";

    private Guid? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<(Guid groupId, bool isValid)> ValidateGroupMembership()
    {
        var groupIdStr = Request.Headers["X-Group-Id"].FirstOrDefault();
        if (!Guid.TryParse(groupIdStr, out var groupId)) return (Guid.Empty, false);

        var userId = GetAuthenticatedUserId();
        if (userId is null) return (Guid.Empty, false);

        var isMember = await dbContext.BettingGroupMembers
            .AnyAsync(m => m.BettingGroupId == groupId && m.UserId == userId.Value);

        return (groupId, isMember);
    }
}
