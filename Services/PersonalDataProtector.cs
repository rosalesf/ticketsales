using Microsoft.AspNetCore.DataProtection;
using System.Security.Cryptography;

namespace TicketSales.Services;

public class PersonalDataProtector : IPersonalDataProtector
{
    private const string EncryptedPrefix = "v2:";
    private readonly byte[] _key;
    private readonly IDataProtector _legacyProtector;

    public PersonalDataProtector(IConfiguration configuration, IDataProtectionProvider dataProtectionProvider)
    {
        var configuredKey = configuration["Encryption:PersonalDataKey"];
        if (string.IsNullOrWhiteSpace(configuredKey)) throw new InvalidOperationException("Configura Encryption:PersonalDataKey para proteger los datos personales.");
        try { _key = Convert.FromBase64String(configuredKey); }
        catch (FormatException exception) { throw new InvalidOperationException("Encryption:PersonalDataKey debe estar en Base64.", exception); }
        if (_key.Length != 32) throw new InvalidOperationException("Encryption:PersonalDataKey debe contener exactamente 32 bytes.");
        _legacyProtector = dataProtectionProvider.CreateProtector("VentaTickets.CustomerPersonalData.v1");
    }

    public string Protect(string value)
    {
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = System.Text.Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];
        using var aes = new AesGcm(_key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);
        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);
        return EncryptedPrefix + Convert.ToBase64String(payload);
    }

    public string Unprotect(string value)
    {
        if (value.StartsWith(EncryptedPrefix, StringComparison.Ordinal))
        {
            try
            {
                var payload = Convert.FromBase64String(value[EncryptedPrefix.Length..]);
                if (payload.Length < 28) throw new CryptographicException("El valor cifrado no es válido.");
                var plaintext = new byte[payload.Length - 28];
                using var aes = new AesGcm(_key, 16);
                aes.Decrypt(payload[..12], payload[28..], payload[12..28], plaintext);
                return System.Text.Encoding.UTF8.GetString(plaintext);
            }
            catch (Exception exception) when (exception is FormatException or CryptographicException)
            {
                throw new CryptographicException("No fue posible descifrar el dato personal.", exception);
            }
        }

        // Compatibilidad con datos guardados antes de usar la clave de configuración.
        try { return _legacyProtector.Unprotect(value); }
        catch (CryptographicException) { return value; }
    }
}
