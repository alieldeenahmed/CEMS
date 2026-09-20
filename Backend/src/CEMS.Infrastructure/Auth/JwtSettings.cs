namespace CEMS.Infrastructure.Auth;

public class JwtSettings
{
    public const int MinimumKeyLength = 32;
    public const int MaximumExpiryMinutes = 24 * 60;

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpiryMinutes { get; set; } = 60;

    /// <summary>
    /// The problems with these settings, if any. Checked at startup so a bad value stops the app with a clear
    /// message instead of surfacing later as tokens that never validate, never expire, or are cheap to forge.
    /// </summary>
    public IReadOnlyList<string> Validate()
    {
        var problems = new List<string>();

        // HS256 wants a key of at least 256 bits; measuring bytes rather than characters is the honest check.
        if (System.Text.Encoding.UTF8.GetByteCount(Key) < MinimumKeyLength)
        {
            problems.Add($"Jwt:Key must be at least {MinimumKeyLength} bytes long.");
        }

        if (string.IsNullOrWhiteSpace(Issuer))
        {
            problems.Add("Jwt:Issuer is required.");
        }

        if (string.IsNullOrWhiteSpace(Audience))
        {
            problems.Add("Jwt:Audience is required.");
        }

        if (ExpiryMinutes is < 1 or > MaximumExpiryMinutes)
        {
            problems.Add($"Jwt:ExpiryMinutes must be between 1 and {MaximumExpiryMinutes} (a token lives until it expires; there is no revocation list).");
        }

        return problems;
    }
}
