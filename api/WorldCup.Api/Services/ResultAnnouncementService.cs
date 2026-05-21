using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;
using WorldCup.Api.DTOs;
using WorldCup.Api.Hubs;
using WorldCup.Api.Models;

namespace WorldCup.Api.Services;

/// <summary>
/// Poster en automatisk melding fra systembrukeren "Resultatservice" i alle liga-chatter
/// når en kamp har fått registrert et resultat. Brukes både fra
/// <see cref="ResultFetcherService"/> (automatisk polling) og admin-pathen i
/// <c>ResultsController.SetResult</c>.
/// </summary>
public sealed class ResultAnnouncementService(
    AppDbContext dbContext,
    IHubContext<ChatHub, IChatClient> chatHub,
    MatchScheduleProvider scheduleProvider,
    TeamCodeMapper teamCodeMapper,
    ILogger<ResultAnnouncementService> logger)
{
    public async Task AnnounceResultAsync(
        int matchId,
        int homeScore,
        int awayScore,
        string? refereeName = null,
        CancellationToken ct = default)
    {
        var match = scheduleProvider.Current.GetMatch(matchId);
        if (match is null)
        {
            logger.LogWarning(
                "Cannot announce result for match {MatchId}: not found in schedule.",
                matchId);
            return;
        }

        var homeName = teamCodeMapper.GetDisplayName(match.HomeTeam);
        var awayName = teamCodeMapper.GetDisplayName(match.AwayTeam);
        var content =
            $"Kampen mellom {homeName} og {awayName} er nå ferdigspilt og resultatet ble: {homeScore}-{awayScore}.";

        // Bruk det faktiske dommernavnet fra oppstrøms-API hvis tilgjengelig.
        // Ellers faller vi tilbake til standardnavnet "Dommeren" via User.Name.
        var displayNameOverride = string.IsNullOrWhiteSpace(refereeName)
            ? null
            : refereeName.Trim();

        var groupIds = await dbContext.BettingGroups
            .Select(g => g.Id)
            .ToListAsync(ct);

        if (groupIds.Count == 0)
        {
            logger.LogInformation(
                "No betting groups found — skipping result announcement for match {MatchId}.",
                matchId);
            return;
        }

        var createdAt = DateTime.UtcNow;
        var messages = groupIds
            .Select(groupId => new ChatMessage
            {
                Id = Guid.NewGuid(),
                BettingGroupId = groupId,
                UserId = SystemUsers.ResultServiceUserId,
                Content = content,
                CreatedAt = createdAt,
                SenderDisplayNameOverride = displayNameOverride
            })
            .ToList();

        dbContext.ChatMessages.AddRange(messages);
        await dbContext.SaveChangesAsync(ct);

        var displayName = displayNameOverride ?? SystemUsers.ResultServiceName;

        foreach (var message in messages)
        {
            var response = new ChatMessageResponse
            {
                Id = message.Id,
                UserId = message.UserId,
                UserName = displayName,
                UserPicture = null,
                Content = message.Content,
                CreatedAt = message.CreatedAt,
                IsDeleted = false,
                IsSystem = true
            };

            await chatHub.Clients
                .Group($"group-{message.BettingGroupId}")
                .MessagePosted(response);
        }

        logger.LogInformation(
            "Posted Dommeren announcement (as '{DisplayName}') for match {MatchId} to {GroupCount} groups.",
            displayName,
            matchId,
            groupIds.Count);
    }
}
