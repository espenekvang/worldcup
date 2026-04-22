using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/invitations")]
public class InvitationsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvitationResponse>>> GetInvitations([FromQuery] Guid? groupId)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var isGlobalAdmin = User.IsInRole("Admin");

        IQueryable<Invitation> query = dbContext.Invitations;

        if (groupId.HasValue)
        {
            if (!isGlobalAdmin && !await IsGroupAdmin(userId.Value, groupId.Value))
                return Forbid();

            query = query.Where(i => i.BettingGroupId == groupId.Value);
        }
        else if (!isGlobalAdmin)
        {
            // Non-global admins can only see invitations for groups they admin
            var adminGroupIds = dbContext.BettingGroupMembers
                .Where(m => m.UserId == userId.Value && m.IsGroupAdmin)
                .Select(m => m.BettingGroupId);

            query = query.Where(i => adminGroupIds.Contains(i.BettingGroupId));
        }

        var invitations = await query
            .OrderBy(i => i.Email)
            .Select(i => new InvitationResponse
            {
                Id = i.Id,
                Email = i.Email,
                BettingGroupId = i.BettingGroupId,
                GroupName = i.BettingGroup.Name,
                CreatedAt = i.CreatedAt
            })
            .ToListAsync();

        return Ok(invitations);
    }

    [HttpPost]
    public async Task<ActionResult<InvitationResponse>> CreateInvitation([FromBody] InvitationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest("E-postadresse er påkrevd.");
        }

        if (request.BettingGroupId == Guid.Empty)
        {
            return BadRequest("BettingGroupId er påkrevd.");
        }

        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        if (!await IsGlobalOrGroupAdmin(userId.Value, request.BettingGroupId))
            return Forbid();

        var group = await dbContext.BettingGroups.FindAsync(request.BettingGroupId);
        if (group is null)
        {
            return NotFound("Liga ikke funnet.");
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await dbContext.Invitations
            .AnyAsync(i => i.Email.ToLower() == normalizedEmail && i.BettingGroupId == request.BettingGroupId);

        if (exists)
        {
            return Conflict("Denne e-postadressen er allerede invitert til denne ligaen.");
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            InvitedByUserId = userId.Value,
            BettingGroupId = request.BettingGroupId
        };

        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new InvitationResponse
        {
            Id = invitation.Id,
            Email = invitation.Email,
            BettingGroupId = invitation.BettingGroupId,
            GroupName = group.Name,
            CreatedAt = invitation.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteInvitation(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var invitation = await dbContext.Invitations.FindAsync(id);
        if (invitation is null) return NotFound();

        if (!await IsGlobalOrGroupAdmin(userId.Value, invitation.BettingGroupId))
            return Forbid();

        dbContext.Invitations.Remove(invitation);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private Guid? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }

    private async Task<bool> IsGroupAdmin(Guid userId, Guid groupId)
    {
        return await dbContext.BettingGroupMembers
            .AnyAsync(m => m.UserId == userId && m.BettingGroupId == groupId && m.IsGroupAdmin);
    }

    private async Task<bool> IsGlobalOrGroupAdmin(Guid userId, Guid groupId)
    {
        if (User.IsInRole("Admin")) return true;
        return await IsGroupAdmin(userId, groupId);
    }
}
