using Microsoft.EntityFrameworkCore;
using WorldCup.Api.Data;

namespace WorldCup.Api.Services;

/// <summary>
/// Kobler <see cref="KnockoutBracketResolver"/> mot databasen og kampskjemaet: leser registrerte
/// resultater, løser opp de sluttspillkampene som nå kan avgjøres, og persisterer eventuelle
/// utfyllinger. Kalles etter enhver endring som kan låse opp en fixture — et nytt resultat, eller
/// en manuell overstyring av lagene i en matende kamp. No-op når ingenting endres.
/// </summary>
public sealed class KnockoutResolutionService(
    AppDbContext dbContext,
    MatchScheduleProvider scheduleProvider,
    MatchFileWriter matchFileWriter,
    KnockoutBracketResolver resolver)
{
    public async Task ResolveAndPersistAsync(CancellationToken ct = default)
    {
        var resultRows = await dbContext.MatchResults
            .Select(r => new { r.MatchId, r.HomeScore, r.AwayScore })
            .AsNoTracking()
            .ToListAsync(ct);
        var scores = resultRows.ToDictionary(r => r.MatchId, r => new MatchScore(r.HomeScore, r.AwayScore));

        var current = scheduleProvider.Current.GetAllMatches();
        var resolved = resolver.Resolve(current, scores);

        var currentById = current.ToDictionary(m => m.Id);
        var changed = resolved.Any(r =>
            currentById.TryGetValue(r.Id, out var before)
            && (r.HomeTeam != before.HomeTeam || r.AwayTeam != before.AwayTeam));

        if (changed)
        {
            await matchFileWriter.WriteAsync(resolved, ct);
        }
    }
}
