using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers;

[ApiController]
[Route("api")]
public class InviteLinksController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet("groups/{groupId:guid}/invite-links")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<InviteLinkResponse>>> ListLinks(Guid groupId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        if (!await IsGlobalOrGroupAdmin(userId.Value, groupId)) return Forbid();

        var group = await dbContext.BettingGroups.FindAsync(groupId);
        if (group is null) return NotFound();

        var links = await dbContext.BettingGroupInviteLinks
            .Where(l => l.BettingGroupId == groupId)
            .OrderByDescending(l => l.CreatedAt)
            .Select(l => new InviteLinkResponse
            {
                Id = l.Id,
                BettingGroupId = l.BettingGroupId,
                GroupName = l.BettingGroup.Name,
                Token = l.Token,
                CreatedAt = l.CreatedAt,
                IsRevoked = l.IsRevoked
            })
            .ToListAsync();

        return Ok(links);
    }

    [HttpPost("groups/{groupId:guid}/invite-links")]
    [Authorize]
    public async Task<ActionResult<InviteLinkResponse>> CreateLink(Guid groupId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        if (!await IsGlobalOrGroupAdmin(userId.Value, groupId)) return Forbid();

        var group = await dbContext.BettingGroups.FindAsync(groupId);
        if (group is null) return NotFound("Liga ikke funnet.");

        var link = new BettingGroupInviteLink
        {
            Id = Guid.NewGuid(),
            BettingGroupId = groupId,
            Token = GenerateToken(),
            CreatedByUserId = userId.Value
        };

        dbContext.BettingGroupInviteLinks.Add(link);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new InviteLinkResponse
        {
            Id = link.Id,
            BettingGroupId = link.BettingGroupId,
            GroupName = group.Name,
            Token = link.Token,
            CreatedAt = link.CreatedAt,
            IsRevoked = link.IsRevoked
        });
    }

    [HttpDelete("invite-links/{id:guid}")]
    [Authorize]
    public async Task<ActionResult> RevokeLink(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var link = await dbContext.BettingGroupInviteLinks.FindAsync(id);
        if (link is null) return NotFound();

        if (!await IsGlobalOrGroupAdmin(userId.Value, link.BettingGroupId)) return Forbid();

        dbContext.BettingGroupInviteLinks.Remove(link);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("invite-links/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<InviteLinkInfoResponse>> GetLinkInfo(string token)
    {
        var link = await dbContext.BettingGroupInviteLinks
            .Include(l => l.BettingGroup)
            .FirstOrDefaultAsync(l => l.Token == token && !l.IsRevoked);

        if (link is null) return NotFound("Ugyldig eller utløpt invitasjonslenke.");

        return Ok(new InviteLinkInfoResponse
        {
            BettingGroupId = link.BettingGroupId,
            GroupName = link.BettingGroup.Name
        });
    }

    [HttpPost("invite-links/{token}/accept")]
    [Authorize]
    public async Task<ActionResult<AcceptInviteLinkResponse>> AcceptLink(string token)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var link = await dbContext.BettingGroupInviteLinks
            .Include(l => l.BettingGroup)
            .FirstOrDefaultAsync(l => l.Token == token && !l.IsRevoked);

        if (link is null) return NotFound("Ugyldig eller utløpt invitasjonslenke.");

        var alreadyMember = await dbContext.BettingGroupMembers
            .AnyAsync(m => m.BettingGroupId == link.BettingGroupId && m.UserId == userId.Value);

        if (!alreadyMember)
        {
            dbContext.BettingGroupMembers.Add(new BettingGroupMember
            {
                Id = Guid.NewGuid(),
                BettingGroupId = link.BettingGroupId,
                UserId = userId.Value
            });
            await dbContext.SaveChangesAsync();
        }

        return Ok(new AcceptInviteLinkResponse
        {
            BettingGroupId = link.BettingGroupId,
            GroupName = link.BettingGroup.Name,
            AlreadyMember = alreadyMember
        });
    }

    private static string GenerateToken()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private Guid? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<bool> IsGlobalOrGroupAdmin(Guid userId, Guid groupId)
    {
        if (User.IsInRole("Admin")) return true;
        return await dbContext.BettingGroupMembers
            .AnyAsync(m => m.UserId == userId && m.BettingGroupId == groupId && m.IsGroupAdmin);
    }
}
