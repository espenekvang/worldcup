namespace WorldCup.Api.Models;

/// <summary>
/// Audit log of outbound calls to the WC2026 API. Used to enforce a rolling 24-hour
/// rate budget so we stay under the upstream provider's 100 calls/day limit, and to
/// give us observability into when we're approaching it.
/// </summary>
public class ApiCallLog
{
    public Guid Id { get; set; }

    public DateTime CalledAt { get; set; }

    public string Endpoint { get; set; } = string.Empty;
}
