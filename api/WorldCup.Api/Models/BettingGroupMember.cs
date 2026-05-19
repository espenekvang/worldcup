namespace WorldCup.Api.Models;

public class BettingGroupMember
{
    public Guid Id { get; set; }
    public Guid BettingGroupId { get; set; }
    public Guid UserId { get; set; }
    public bool IsGroupAdmin { get; set; }

    /// <summary>
    /// True når medlemmet har betalt avgift for å være med i en betalt liga.
    /// Selve betalingen skjer utenfor løsningen; admin / liga-admin registrerer status her.
    /// </summary>
    public bool HasPaid { get; set; }

    /// <summary>
    /// Tidspunkt for når betaling ble registrert (eller null hvis ikke betalt).
    /// </summary>
    public DateTime? PaidAt { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public BettingGroup BettingGroup { get; set; } = null!;
    public User User { get; set; } = null!;
}
