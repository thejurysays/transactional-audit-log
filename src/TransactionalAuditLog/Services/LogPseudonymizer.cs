using System.Security.Cryptography;
using System.Text;

namespace TransactionalAuditLog.Services;

public sealed class LogPseudonymizer
{
    private const string PseudonymKeyConfig = "Logging:PseudonymKey";

    private readonly byte[] _keyBytes;

    public LogPseudonymizer(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var key = configuration.GetValue<string>(PseudonymKeyConfig);
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"'{PseudonymKeyConfig}' is required but not configured. " +
                "Set a secret value via environment variable Logging__PseudonymKey.");

        _keyBytes = Encoding.UTF8.GetBytes(key);
    }

    // First 8 bytes (16 hex chars) of HMAC-SHA256 — sufficient uniqueness for log correlation;
    // same input always produces the same pseudonym, but cannot be reversed without the key.
    public string Pseudonymize(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        using var hmac = new HMACSHA256(_keyBytes);
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash, 0, 8).ToLowerInvariant();
    }
}
