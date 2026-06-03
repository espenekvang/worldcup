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
[Authorize(Roles = "Admin")]
[Route("api/admin/broadcast")]
public class BroadcastController(AppDbContext dbContext, IHubContext<ChatHub, IChatClient> chatHub) : ControllerBase
{
    private const string SenderName = "📣 Bakrommet";
    private const int MaxContentLength = 500;

    [HttpPost]
    public async Task<ActionResult> BroadcastToAllGroups([FromBody] BroadcastMessageDto request)
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdClaim, out var userId)) return Unauthorized();

        var content = (request.Content ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(content))
            return BadRequest("Meldingen kan ikke være tom.");
        if (content.Length > MaxContentLength)
            return BadRequest($"Meldingen kan maks være {MaxContentLength} tegn.");

        var groups = await dbContext.BettingGroups
            .AsNoTracking()
            .ToListAsync();

        var user = await dbContext.Users.FindAsync(userId);
        if (user is null) return Unauthorized();

        var now = DateTime.UtcNow;

        foreach (var group in groups)
        {
            var message = new ChatMessage
            {
                Id = Guid.NewGuid(),
                BettingGroupId = group.Id,
                UserId = userId,
                Content = content,
                CreatedAt = now,
                SenderDisplayNameOverride = SenderName,
            };

            dbContext.ChatMessages.Add(message);

            var response = new ChatMessageResponse
            {
                Id = message.Id,
                UserId = message.UserId,
                UserName = SenderName,
                UserPicture = user.Picture,
                Content = content,
                CreatedAt = now,
                IsDeleted = false,
                IsSystem = true,
            };

            await chatHub.Clients.Group($"group-{group.Id}").MessagePosted(response);
        }

        await dbContext.SaveChangesAsync();

        return Ok(new { groupCount = groups.Count });
    }
}
