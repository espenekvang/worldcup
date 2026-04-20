using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Google.Apis.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext dbContext, IConfiguration configuration) : ControllerBase
{
    [HttpPost("google")]
    public async Task<ActionResult<AuthResponse>> GoogleLogin([FromBody] GoogleLoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.IdToken))
        {
            return BadRequest("IdToken is required.");
        }

        var googleClientId = configuration["Google:ClientId"];
        if (string.IsNullOrWhiteSpace(googleClientId))
        {
            return BadRequest("Google Client ID is not configured.");
        }

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(request.IdToken, new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = [googleClientId]
            });
        }
        catch (InvalidJwtException)
        {
            return Unauthorized();
        }

        var adminEmail = configuration["Admin:Email"]?.ToLowerInvariant();
        var isAdmin = string.Equals(payload.Email, adminEmail, StringComparison.OrdinalIgnoreCase);

        if (!isAdmin)
        {
            var isInvited = await dbContext.Invitations
                .AnyAsync(i => i.Email.ToLower() == payload.Email.ToLower());

            if (!isInvited)
            {
                return StatusCode(403, "Du er ikke invitert. Be administrator om en invitasjon.");
            }
        }

        var user = await dbContext.Users.SingleOrDefaultAsync(u => u.GoogleId == payload.Subject);

        if (user is null)
        {
            user = new User
            {
                Id = Guid.NewGuid(),
                GoogleId = payload.Subject,
                Email = payload.Email,
                Name = payload.Name,
                Picture = payload.Picture,
                IsAdmin = isAdmin
            };

            dbContext.Users.Add(user);
        }
        else
        {
            user.Email = payload.Email;
            user.Name = payload.Name;
            user.Picture = payload.Picture;
            user.IsAdmin = isAdmin;
        }

        await dbContext.SaveChangesAsync();

        // Fresh-DB bootstrap: if admin and no groups exist, create Default group
        if (isAdmin && !await dbContext.BettingGroups.AnyAsync())
        {
            var defaultGroup = new BettingGroup
            {
                Id = Guid.NewGuid(),
                Name = "Default",
                CreatedByUserId = user.Id
            };

            dbContext.BettingGroups.Add(defaultGroup);
            dbContext.BettingGroupMembers.Add(new BettingGroupMember
            {
                Id = Guid.NewGuid(),
                BettingGroupId = defaultGroup.Id,
                UserId = user.Id
            });

            await dbContext.SaveChangesAsync();
        }

        // Auto-join: consume pending invitations
        var pendingInvitations = await dbContext.Invitations
            .Where(i => i.Email.ToLower() == user.Email.ToLower())
            .ToListAsync();

        foreach (var invitation in pendingInvitations)
        {
            var alreadyMember = await dbContext.BettingGroupMembers
                .AnyAsync(m => m.BettingGroupId == invitation.BettingGroupId && m.UserId == user.Id);

            if (!alreadyMember)
            {
                dbContext.BettingGroupMembers.Add(new BettingGroupMember
                {
                    Id = Guid.NewGuid(),
                    BettingGroupId = invitation.BettingGroupId,
                    UserId = user.Id
                });
            }

            // Consume the invitation
            dbContext.Invitations.Remove(invitation);
        }

        if (pendingInvitations.Count > 0)
        {
            await dbContext.SaveChangesAsync();
        }

        // Fetch user's groups for response
        var groups = await dbContext.BettingGroupMembers
            .Where(m => m.UserId == user.Id)
            .Select(m => new BettingGroupResponse(
                m.BettingGroupId,
                m.BettingGroup.Name,
                m.BettingGroup.Members.Count,
                m.BettingGroup.CreatedAt))
            .ToListAsync();

        var jwtKey = configuration["Jwt:Key"]
            ?? throw new InvalidOperationException("JWT signing key is not configured.");
        var jwtIssuer = configuration["Jwt:Issuer"]
            ?? throw new InvalidOperationException("JWT issuer is not configured.");
        var jwtAudience = configuration["Jwt:Audience"]
            ?? throw new InvalidOperationException("JWT audience is not configured.");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.Name),
            new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "User")
        };

        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
            SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: jwtIssuer,
            audience: jwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(7),
            signingCredentials: credentials);

        var response = new AuthResponse
        {
            Token = new JwtSecurityTokenHandler().WriteToken(token),
            Email = user.Email,
            Name = user.Name,
            Picture = user.Picture,
            IsAdmin = user.IsAdmin,
            Groups = groups
        };

        return Ok(response);
    }
}
