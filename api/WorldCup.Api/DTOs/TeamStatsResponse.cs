namespace WorldCup.Api.DTOs;

/// <summary>
/// Statistikk for ett lag, brukt på matchdetalj-siden så man kan se form,
/// nøkkelinfo og snittall før man tipper. Data hentes fra en seed-JSON
/// (eventuelt overstyrt av en ekstern API hvis konfigurert).
/// </summary>
public sealed record TeamStatsResponse(
    string TeamCode,
    int? FifaRank,
    string? Manager,
    string? StarPlayer,
    string? PreferredFormation,
    /// <summary>Snitt mål scoret per kamp i de siste ~10 kampene.</summary>
    double? GoalsScoredAvg,
    /// <summary>Snitt mål sluppet inn per kamp i de siste ~10 kampene.</summary>
    double? GoalsConcededAvg,
    /// <summary>Form-streng, f.eks. "WWDWL" (eldste først).</summary>
    string? RecentForm,
    /// <summary>Siste oppvarmings-/kvalik-/vennskapskamper, nyeste først.</summary>
    IReadOnlyList<RecentMatchEntry> RecentMatches,
    /// <summary>Viktige skader/suspensjoner som påvirker neste kamp.</summary>
    IReadOnlyList<string> KeyAbsences,
    /// <summary>Resultater fra forrige VM (kortform), eller null hvis ikke deltatt.</summary>
    string? LastWorldCupResult);

public sealed record RecentMatchEntry(
    string Date,
    string Opponent,
    /// <summary>"home", "away" eller "neutral".</summary>
    string Venue,
    int GoalsFor,
    int GoalsAgainst,
    /// <summary>"W", "D" eller "L".</summary>
    string Result,
    string Competition);
