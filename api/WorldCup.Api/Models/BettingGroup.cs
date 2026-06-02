namespace WorldCup.Api.Models;

public class BettingGroup
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// True når dette er en betalt liga (krever at hver deltaker har betalt innsats før de kan bette).
    /// Settes kun ved opprettelse, og kun når feature-flagget "PaidLeagues" er på (kontrolleres av global admin).
    /// </summary>
    public bool IsPaid { get; set; }

    /// <summary>
    /// Innsats per deltaker i NOK. Kun relevant når <see cref="IsPaid"/> er true.
    /// </summary>
    public decimal EntryFee { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<BettingGroupMember> Members { get; set; } = [];
}
