namespace WorldCup.Api.Services;

/// <summary>
/// Stabile identifikatorer for systembrukere som ikke representerer ekte personer,
/// men som likevel må eksistere som <see cref="Models.User"/>-rader (FK-krav fra
/// f.eks. <see cref="Models.ChatMessage"/>).
/// </summary>
public static class SystemUsers
{
    // Deterministisk GUID — må aldri endres etter at den er deployet, ellers
    // mister vi koblingen til eksisterende chat-meldinger.
    public static readonly Guid ResultServiceUserId =
        new("11111111-0000-0000-0000-000000000001");

    public const string ResultServiceName = "Dommeren";
    public const string ResultServiceEmail = "dommeren@system.local";
    public const string ResultServiceGoogleId = "system:resultservice";
}
