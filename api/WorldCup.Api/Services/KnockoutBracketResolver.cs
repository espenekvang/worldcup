using System.Text.RegularExpressions;

namespace WorldCup.Api.Services;

/// <summary>
/// Fyller inn lag i sluttspillkamper lokalt ut fra registrerte resultater, uten å spørre
/// oppstrøms-API-et. Håndterer de kampnummer-baserte placeholderne («Vinner kamp N» /
/// «Taper kamp N») som binder én sluttspillkamp til en tidligere — dvs. kjeden fra
/// 8-delsfinale og oppover, samt bronse- og finalekampen.
///
/// Dette dekker hullet der resultater settes manuelt: <c>ResultFetcherService</c> løser bare
/// opp fixtures ved å spørre oppstrøms-bracket-en, så et manuelt registrert kvartfinale-resultat
/// avanserer ellers aldri semifinalen. Runde-32-inngangen («2. plass gruppe E») er gruppestilling-
/// basert og håndteres ikke her — den kommer fra oppstrøms-feeden.
/// </summary>
public sealed class KnockoutBracketResolver
{
    private static readonly Regex WinnerPlaceholderPattern =
        new(@"^\s*vinner\s+kamp\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LoserPlaceholderPattern =
        new(@"^\s*taper\s+kamp\s+(\d+)\s*$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Returnerer kampskjemaet med eventuelle nye lag-utfyllinger. Itererer til et fikspunkt slik
    /// at en kjede løses i ett kall (et kvartfinale-resultat løser semifinalen, hvis semifinalen
    /// har et resultat løses finalen i neste runde). Uendret input returneres uendret.
    /// </summary>
    public IReadOnlyList<MatchEntry> Resolve(
        IReadOnlyList<MatchEntry> matches,
        IReadOnlyDictionary<int, MatchScore> resultsByMatchId)
    {
        var byId = matches.ToDictionary(m => m.Id);

        bool changed;
        do
        {
            changed = false;
            foreach (var id in byId.Keys.ToList())
            {
                var match = byId[id];

                if (IsGroupStage(match.Stage)) continue;
                if (match.ManualOverride) continue;          // respekter admins egne overstyringer
                if (!match.AreTeamsUndetermined) continue;    // allerede oppløst

                var newHome = match.HomeTeam ?? ResolveSlot(match.HomePlaceholder, byId, resultsByMatchId);
                var newAway = match.AwayTeam ?? ResolveSlot(match.AwayPlaceholder, byId, resultsByMatchId);

                if (newHome == match.HomeTeam && newAway == match.AwayTeam) continue;

                // Ikke skriv et umulig oppsett (samme lag på begge sider) — samme vern som
                // oppstrøms-fyllingen bruker.
                if (MatchEntry.WouldDuplicateTeam(newHome, newAway)) continue;

                byId[id] = CloneWith(match, newHome, newAway);
                changed = true;
            }
        }
        while (changed);

        return matches.Select(m => byId[m.Id]).ToList();
    }

    /// <summary>
    /// Løser en enkelt placeholder («Vinner kamp N» / «Taper kamp N») til en lagkode, eller null
    /// når den ikke kan avgjøres ennå: matende kamp mangler resultat, har uoppløste lag, eller
    /// endte uavgjort (straffekonk. — vinneren kan ikke utledes fra sluttresultatet alene).
    /// </summary>
    private static string? ResolveSlot(
        string? placeholder,
        IReadOnlyDictionary<int, MatchEntry> byId,
        IReadOnlyDictionary<int, MatchScore> resultsByMatchId)
    {
        if (string.IsNullOrWhiteSpace(placeholder)) return null;

        var wantWinner = WinnerPlaceholderPattern.Match(placeholder);
        var wantLoser = wantWinner.Success ? Match.Empty : LoserPlaceholderPattern.Match(placeholder);

        var m = wantWinner.Success ? wantWinner : wantLoser;
        if (!m.Success) return null;

        var feederId = int.Parse(m.Groups[1].Value);
        if (!byId.TryGetValue(feederId, out var feeder)) return null;
        if (feeder.HomeTeam is null || feeder.AwayTeam is null) return null; // matende lag ikke oppløst
        if (!resultsByMatchId.TryGetValue(feederId, out var score)) return null; // ikke spilt ennå
        if (score.HomeScore == score.AwayScore) return null; // uavgjort — vinner ukjent

        var homeWon = score.HomeScore > score.AwayScore;
        return wantWinner.Success
            ? (homeWon ? feeder.HomeTeam : feeder.AwayTeam)
            : (homeWon ? feeder.AwayTeam : feeder.HomeTeam);
    }

    private static MatchEntry CloneWith(MatchEntry match, string? homeTeam, string? awayTeam) => new()
    {
        Id = match.Id,
        Date = match.Date,
        Stage = match.Stage,
        HomeTeam = homeTeam,
        AwayTeam = awayTeam,
        HomePlaceholder = match.HomePlaceholder,
        AwayPlaceholder = match.AwayPlaceholder,
        Group = match.Group,
        VenueId = match.VenueId,
        ManualOverride = match.ManualOverride,
    };

    private static bool IsGroupStage(string? stage) =>
        stage is not null && stage.StartsWith("group", StringComparison.OrdinalIgnoreCase);
}

/// <summary>Sluttresultat for en kamp (kun målene; brukes til vinner/taper-utledning).</summary>
public readonly record struct MatchScore(int HomeScore, int AwayScore);
