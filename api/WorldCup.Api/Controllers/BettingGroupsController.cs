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
[Route("api/groups")]
public class BettingGroupsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<BettingGroupResponse>>> GetGroups()
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var isAdmin = User.IsInRole("Admin");

        IQueryable<BettingGroup> query = dbContext.BettingGroups;

        if (!isAdmin)
        {
            var memberGroupIds = dbContext.BettingGroupMembers
                .Where(m => m.UserId == userId.Value)
                .Select(m => m.BettingGroupId);

            query = query.Where(g => memberGroupIds.Contains(g.Id));
        }

        var groups = await query
            .OrderBy(g => g.Name)
            .Select(g => new BettingGroupResponse(
                g.Id,
                g.Name,
                g.Members.Count,
                g.CreatedAt))
            .AsNoTracking()
            .ToListAsync();

        return Ok(groups);
    }

    [HttpGet("/api/admin/groups")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<BettingGroupResponse>>> GetAllGroups()
    {
        var groups = await dbContext.BettingGroups
            .OrderBy(g => g.Name)
            .Select(g => new BettingGroupResponse(
                g.Id,
                g.Name,
                g.Members.Count,
                g.CreatedAt))
            .AsNoTracking()
            .ToListAsync();

        return Ok(groups);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BettingGroupResponse>> CreateGroup([FromBody] CreateBettingGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Gruppenavn er påkrevd.");

        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var group = new BettingGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedByUserId = userId.Value
        };

        dbContext.BettingGroups.Add(group);

        var memberCount = 0;

        if (request.JoinGroup)
        {
            dbContext.BettingGroupMembers.Add(new BettingGroupMember
            {
                Id = Guid.NewGuid(),
                BettingGroupId = group.Id,
                UserId = userId.Value
            });
            memberCount = 1;
        }

        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created,
            new BettingGroupResponse(group.Id, group.Name, memberCount, group.CreatedAt));
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BettingGroupResponse>> UpdateGroup(Guid id, [FromBody] UpdateBettingGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Gruppenavn er påkrevd.");

        var group = await dbContext.BettingGroups
            .Include(g => g.Members)
            .FirstOrDefaultAsync(g => g.Id == id);

        if (group is null) return NotFound();

        group.Name = request.Name.Trim();
        await dbContext.SaveChangesAsync();

        return Ok(new BettingGroupResponse(group.Id, group.Name, group.Members.Count, group.CreatedAt));
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> DeleteGroup(Guid id)
    {
        var group = await dbContext.BettingGroups.FindAsync(id);
        if (group is null) return NotFound();

        dbContext.BettingGroups.Remove(group);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<BettingGroupMemberResponse>>> GetMembers(Guid id)
    {
        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        if (!await IsGlobalOrGroupAdmin(userId.Value, id))
            return Forbid();

        var groupExists = await dbContext.BettingGroups.AnyAsync(g => g.Id == id);
        if (!groupExists) return NotFound();

        var members = await dbContext.BettingGroupMembers
            .Where(m => m.BettingGroupId == id)
            .Select(m => new BettingGroupMemberResponse(
                m.UserId,
                m.User.Name,
                m.User.Email,
                m.User.Picture,
                m.IsGroupAdmin,
                m.JoinedAt))
            .OrderBy(m => m.Name)
            .AsNoTracking()
            .ToListAsync();

        return Ok(members);
    }

    [HttpPost("{id:guid}/members")]
    [Authorize]
    public async Task<ActionResult<BettingGroupMemberResponse>> AddMember(Guid id, [FromBody] AddGroupMemberRequest request)
    {
        var callerUserId = GetAuthenticatedUserId();
        if (callerUserId is null) return Unauthorized();

        if (!await IsGlobalOrGroupAdmin(callerUserId.Value, id))
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Email))
            return BadRequest("E-postadresse er påkrevd.");

        var groupExists = await dbContext.BettingGroups.AnyAsync(g => g.Id == id);
        if (!groupExists) return NotFound("Gruppe ikke funnet.");

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
        if (user is null)
            return NotFound("Bruker ikke funnet. Inviter dem først.");

        var alreadyMember = await dbContext.BettingGroupMembers
            .AnyAsync(m => m.BettingGroupId == id && m.UserId == user.Id);
        if (alreadyMember)
            return Conflict("Brukeren er allerede medlem av denne ligaen.");

        var member = new BettingGroupMember
        {
            Id = Guid.NewGuid(),
            BettingGroupId = id,
            UserId = user.Id
        };

        dbContext.BettingGroupMembers.Add(member);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created,
            new BettingGroupMemberResponse(user.Id, user.Name, user.Email, user.Picture, false, member.JoinedAt));
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize]
    public async Task<ActionResult> RemoveMember(Guid id, Guid userId)
    {
        var callerUserId = GetAuthenticatedUserId();
        if (callerUserId is null) return Unauthorized();

        if (!await IsGlobalOrGroupAdmin(callerUserId.Value, id))
            return Forbid();

        var member = await dbContext.BettingGroupMembers
            .FirstOrDefaultAsync(m => m.BettingGroupId == id && m.UserId == userId);

        if (member is null) return NotFound();

        dbContext.BettingGroupMembers.Remove(member);

        // Also delete any matching invitation to prevent auto-rejoin on next login
        var user = await dbContext.Users.FindAsync(userId);
        if (user is not null)
        {
            var invitation = await dbContext.Invitations
                .FirstOrDefaultAsync(i => i.Email.ToLower() == user.Email.ToLower() && i.BettingGroupId == id);

            if (invitation is not null)
            {
                dbContext.Invitations.Remove(invitation);
            }
        }

        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    [HttpPut("{id:guid}/members/{userId:guid}/admin")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> ToggleGroupAdmin(Guid id, Guid userId, [FromBody] SetGroupAdminRequest request)
    {
        var member = await dbContext.BettingGroupMembers
            .FirstOrDefaultAsync(m => m.BettingGroupId == id && m.UserId == userId);

        if (member is null) return NotFound();

        member.IsGroupAdmin = request.IsGroupAdmin;
        await dbContext.SaveChangesAsync();

        return NoContent();
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
