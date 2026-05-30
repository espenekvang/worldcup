using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.FeatureManagement;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/groups")]
public class BettingGroupsController(AppDbContext dbContext, IFeatureManager featureManager) : ControllerBase
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
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.CreatedAt,
                g.IsPaid,
                g.EntryFee,
                MemberCount = g.Members.Count,
                PaidMemberCount = g.Members.Count(m => m.HasPaid),
                CurrentUserHasPaid = g.Members.Any(m => m.UserId == userId.Value && m.HasPaid)
            })
            .AsNoTracking()
            .ToListAsync();

        var response = groups
            .Select(g => new BettingGroupResponse(
                g.Id,
                g.Name,
                g.MemberCount,
                g.CreatedAt,
                g.IsPaid,
                g.EntryFee,
                g.IsPaid ? g.EntryFee * g.PaidMemberCount : 0m,
                g.PaidMemberCount,
                g.CurrentUserHasPaid))
            .ToList();

        return Ok(response);
    }

    [HttpGet("/api/admin/groups")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<BettingGroupResponse>>> GetAllGroups()
    {
        var userId = GetAuthenticatedUserId() ?? Guid.Empty;

        var groups = await dbContext.BettingGroups
            .OrderBy(g => g.Name)
            .Select(g => new
            {
                g.Id,
                g.Name,
                g.CreatedAt,
                g.IsPaid,
                g.EntryFee,
                MemberCount = g.Members.Count,
                PaidMemberCount = g.Members.Count(m => m.HasPaid),
                CurrentUserHasPaid = g.Members.Any(m => m.UserId == userId && m.HasPaid)
            })
            .AsNoTracking()
            .ToListAsync();

        var response = groups
            .Select(g => new BettingGroupResponse(
                g.Id,
                g.Name,
                g.MemberCount,
                g.CreatedAt,
                g.IsPaid,
                g.EntryFee,
                g.IsPaid ? g.EntryFee * g.PaidMemberCount : 0m,
                g.PaidMemberCount,
                g.CurrentUserHasPaid))
            .ToList();

        return Ok(response);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<BettingGroupResponse>> CreateGroup([FromBody] CreateBettingGroupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest("Gruppenavn er påkrevd.");

        var userId = GetAuthenticatedUserId();
        if (userId is null) return Unauthorized();

        var isPaid = request.IsPaid;
        var entryFee = request.EntryFee;

        if (isPaid)
        {
            // Kun mulig å opprette betalt liga når PaidLeagues-flagget er på.
            // Flagget styres av global admin via konfigurasjon (appsettings / Azure App Configuration)
            // – lokal admin har ikke tilgang, og lokal admin kan uansett ikke opprette ligaer.
            var paidLeaguesEnabled = await featureManager.IsEnabledAsync("PaidLeagues");
            if (!paidLeaguesEnabled)
            {
                return BadRequest("Funksjonen for betalte ligaer er ikke aktivert.");
            }

            if (entryFee <= 0m)
            {
                return BadRequest("Avgift må være større enn 0 for en betalt liga.");
            }
        }
        else
        {
            entryFee = 0m;
        }

        var group = new BettingGroup
        {
            Id = Guid.NewGuid(),
            Name = request.Name.Trim(),
            CreatedByUserId = userId.Value,
            IsPaid = isPaid,
            EntryFee = entryFee
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
            new BettingGroupResponse(
                group.Id,
                group.Name,
                memberCount,
                group.CreatedAt,
                group.IsPaid,
                group.EntryFee,
                0m,
                0,
                false));
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

        // Konvertering / oppdatering av betalt-status
        if (request.IsPaid.HasValue)
        {
            var desiredPaid = request.IsPaid.Value;

            // Tilbakeføring fra betalt til gratis er ikke tillatt – det ville mistet betalingsregistreringer.
            if (group.IsPaid && !desiredPaid)
            {
                return BadRequest("Kan ikke konvertere en betalt liga tilbake til gratis.");
            }

            // Aktivering av betalt liga krever at feature-flagget er på (kun global admin styrer dette).
            if (!group.IsPaid && desiredPaid)
            {
                var paidLeaguesEnabled = await featureManager.IsEnabledAsync("PaidLeagues");
                if (!paidLeaguesEnabled)
                {
                    return BadRequest("Funksjonen for betalte ligaer er ikke aktivert.");
                }

                var newFee = request.EntryFee ?? 0m;
                if (newFee <= 0m)
                {
                    return BadRequest("Avgift må være større enn 0 for en betalt liga.");
                }

                group.IsPaid = true;
                group.EntryFee = newFee;
            }
            else if (group.IsPaid && desiredPaid && request.EntryFee.HasValue)
            {
                // Endring av avgift kun tillatt så lenge ingen har betalt ennå.
                if (group.Members.Any(m => m.HasPaid))
                {
                    return BadRequest("Avgiften kan ikke endres etter at noen har betalt.");
                }

                if (request.EntryFee.Value <= 0m)
                {
                    return BadRequest("Avgift må være større enn 0 for en betalt liga.");
                }

                var oldFee = group.EntryFee;
                group.EntryFee = request.EntryFee.Value;
                Console.WriteLine($"[UpdateGroup] Changing EntryFee for group {group.Id} from {oldFee} to {group.EntryFee}");
            }
        }

        await dbContext.SaveChangesAsync();

        var userId = GetAuthenticatedUserId() ?? Guid.Empty;
        var paidCount = group.Members.Count(m => m.HasPaid);
        var currentUserPaid = group.Members.Any(m => m.UserId == userId && m.HasPaid);

        return Ok(new BettingGroupResponse(
            group.Id,
            group.Name,
            group.Members.Count,
            group.CreatedAt,
            group.IsPaid,
            group.EntryFee,
            group.IsPaid ? group.EntryFee * paidCount : 0m,
            paidCount,
            currentUserPaid));
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
            .OrderBy(m => m.User.Name)
            .Select(m => new BettingGroupMemberResponse(
                m.UserId,
                m.User.Name,
                m.User.Email,
                m.User.Picture,
                m.IsGroupAdmin,
                m.JoinedAt,
                m.HasPaid,
                m.PaidAt))
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
            new BettingGroupMemberResponse(user.Id, user.Name, user.Email, user.Picture, false, member.JoinedAt, false, null));
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

    /// <summary>
    /// Registrer eller fjern betalt status for et medlem i en betalt liga.
    /// Både global admin og liga-admin kan kalle denne (selve betalingen skjer utenfor løsningen).
    /// </summary>
    [HttpPut("{id:guid}/members/{userId:guid}/payment")]
    [Authorize]
    public async Task<ActionResult> SetMemberPaid(Guid id, Guid userId, [FromBody] SetMemberPaidRequest request)
    {
        var callerUserId = GetAuthenticatedUserId();
        if (callerUserId is null) return Unauthorized();

        if (!await IsGlobalOrGroupAdmin(callerUserId.Value, id))
            return Forbid();

        var group = await dbContext.BettingGroups.FirstOrDefaultAsync(g => g.Id == id);
        if (group is null) return NotFound("Liga ikke funnet.");

        if (!group.IsPaid)
            return BadRequest("Denne ligaen er ikke en betalt liga.");

        var member = await dbContext.BettingGroupMembers
            .FirstOrDefaultAsync(m => m.BettingGroupId == id && m.UserId == userId);

        if (member is null) return NotFound("Medlem ikke funnet.");

        member.HasPaid = request.HasPaid;
        member.PaidAt = request.HasPaid ? DateTime.UtcNow : null;

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
