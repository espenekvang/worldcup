using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;

namespace WorldCup.Api.Hubs;

public interface IChatClient
{
    Task MessagePosted(ChatMessageResponse message);
    Task MessageDeleted(ChatDeletedEventDto evt);
}

[Authorize]
public class ChatHub(AppDbContext dbContext) : Hub<IChatClient>
{
    public async Task JoinGroup(Guid groupId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) throw new HubException("Unauthorized");

        var isMember = await dbContext.BettingGroupMembers
            .AnyAsync(m => m.BettingGroupId == groupId && m.UserId == userId.Value);

        if (!isMember) throw new HubException("Not a member of this group");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(groupId));
    }

    public async Task LeaveGroup(Guid groupId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(groupId));
    }

    private static string GroupName(Guid groupId) => $"group-{groupId}";

    private Guid? GetAuthenticatedUserId()
    {
        var userIdClaim = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
