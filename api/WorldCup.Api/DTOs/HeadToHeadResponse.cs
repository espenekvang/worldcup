namespace WorldCup.Api.DTOs;

/// <summary>
/// Historikk for to lags innbyrdes møter. Brukes på matchdetalj-siden for å
/// gi tipperen perspektiv på hvordan lagene pleier å gjøre det mot hverandre.
///
/// Lagkodene returneres i samme rekkefølge som klienten ba om (TeamA = "home"
/// fra klientens perspektiv), men de underliggende dataene er lagret i en
/// kanonisk (alfabetisk) rekkefølge — service-laget speilvender ved behov.
/// </summary>
public sealed record HeadToHeadResponse(
    string TeamA,
    string TeamB,
    int TotalMatches,
    int TeamAWins,
    int Draws,
    int TeamBWins,
    int TeamAGoals,
    int TeamBGoals,
    /// <summary>Siste møter (nyeste først), maks 5 entries.</summary>
    IReadOnlyList<HeadToHeadMatch> RecentMatches);

public sealed record HeadToHeadMatch(
    string Date,
    /// <summary>3-bokstavs lagkode for hjemmelaget i denne kampen.</summary>
    string HomeTeam,
    string AwayTeam,
    int HomeScore,
    int AwayScore,
    string Competition,
    string? Venue);
