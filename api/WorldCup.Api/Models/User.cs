namespace WorldCup.Api.Models;

public class User
{
    public Guid Id { get; set; }
    public string GoogleId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Brukerens selvvalgte visningsnavn. Når satt brukes dette i stedet for
    /// <see cref="Name"/> (fornavn/fullt navn) overalt brukerens navn vises i appen.
    /// Null/tom betyr at vi faller tilbake til Google-navnet.
    /// </summary>
    public string? DisplayName { get; set; }

    public string? Picture { get; set; }
    public bool IsAdmin { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
