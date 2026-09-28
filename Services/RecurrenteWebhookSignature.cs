using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace TicketSales.Services;

public static class RecurrenteWebhookSignature
{
    public static bool Verify(string secret, string id, string timestamp, string signatures, byte[] body, DateTimeOffset now)
    {
        if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(id) ||
            !long.TryParse(timestamp, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) ||
            Math.Abs((decimal)now.ToUnixTimeSeconds() - seconds) > 300) return false;
        try
        {
            var key = Convert.FromBase64String(secret.StartsWith("whsec_", StringComparison.Ordinal) ? secret[6..] : secret);
            if (key.Length < 16) return false;
            var prefix = Encoding.UTF8.GetBytes(id + "." + timestamp + ".");
            var signed = new byte[prefix.Length + body.Length];
            prefix.CopyTo(signed, 0); body.CopyTo(signed, prefix.Length);
            var expected = HMACSHA256.HashData(key, signed);
            foreach (var signature in signatures.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (!signature.StartsWith("v1,", StringComparison.Ordinal)) continue;
                try { if (CryptographicOperations.FixedTimeEquals(expected, Convert.FromBase64String(signature[3..]))) return true; }
                catch (FormatException) { }
            }
        }
        catch (FormatException) { }
        return false;
    }
}
