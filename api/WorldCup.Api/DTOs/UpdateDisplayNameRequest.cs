namespace WorldCup.Api.DTOs;

public class UpdateDisplayNameRequest
{
    /// <summary>
    /// Nytt visningsnavn. Tom streng eller null nullstiller visningsnavnet slik at
    /// brukeren faller tilbake til Google-navnet.
    /// </summary>
    public string? DisplayName { get; set; }
}

public class UpdateDisplayNameResponse
{
    /// <summary>Lagret visningsnavn (null hvis nullstilt).</summary>
    public string? DisplayName { get; set; }
}
