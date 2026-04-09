using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers;

[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/invitations")]
public class InvitationsController(AppDbContext dbContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<InvitationResponse>>> GetInvitations()
    {
        var invitations = await dbContext.Invitations
            .OrderBy(i => i.Email)
            .Select(i => new InvitationResponse
            {
                Id = i.Id,
                Email = i.Email,
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

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var exists = await dbContext.Invitations
            .AnyAsync(i => i.Email.ToLower() == normalizedEmail);

        if (exists)
        {
            return Conflict("Denne e-postadressen er allerede invitert.");
        }

        var userId = GetAuthenticatedUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var invitation = new Invitation
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            InvitedByUserId = userId.Value
        };

        dbContext.Invitations.Add(invitation);
        await dbContext.SaveChangesAsync();

        return StatusCode(StatusCodes.Status201Created, new InvitationResponse
        {
            Id = invitation.Id,
            Email = invitation.Email,
            CreatedAt = invitation.CreatedAt
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteInvitation(Guid id)
    {
        var invitation = await dbContext.Invitations.FindAsync(id);

        if (invitation is null)
        {
            return NotFound();
        }

        dbContext.Invitations.Remove(invitation);
        await dbContext.SaveChangesAsync();

        return NoContent();
    }

    private Guid? GetAuthenticatedUserId()
    {
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(userIdClaim, out var userId) ? userId : null;
    }
}
