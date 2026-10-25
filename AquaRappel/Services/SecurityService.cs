using System.Security.Cryptography;
using System.Text;

namespace AquaRappel.Services;

public interface ISecurityService
{
    string HashPassword(string password, out string salt);
    bool Verify(string password, string salt, string expectedHash);
    byte[] Encrypt(byte[] plain);
    byte[] Decrypt(byte[] blob);
    string? GetSession();
    void SetSession(string? email);
}

/// <summary>
/// Sécurité : hachage PBKDF2 des mots de passe, chiffrement AES-GCM du fichier de données,
/// clé et session conservées dans le coffre sécurisé d'Android (SecureStorage).
/// </summary>
public class SecurityService : ISecurityService
{
    private const int Iterations = 100_000;
    private const string KeyName = "aq_data_key";
    private const string SessionName = "aq_session";
    private byte[]? _key;

    // ---------- Mots de passe ----------
    public string HashPassword(string password, out string salt)
    {
        byte[] s = RandomNumberGenerator.GetBytes(16);
        salt = Convert.ToBase64String(s);
        return Convert.ToBase64String(Derive(password, s));
    }

    public bool Verify(string password, string salt, string expectedHash)
    {
        byte[] actual = Derive(password, Convert.FromBase64String(salt));
        byte[] expected = Convert.FromBase64String(expectedHash);
        return CryptographicOperations.FixedTimeEquals(actual, expected);   // comparaison à temps constant
    }

    private static byte[] Derive(string password, byte[] salt)
        => Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, HashAlgorithmName.SHA256, 32);

    // ---------- Chiffrement du fichier ----------
    private byte[] Key()
    {
        if (_key != null) return _key;

        string? stored = Run(() => SecureStorage.Default.GetAsync(KeyName));
        if (string.IsNullOrEmpty(stored))
        {
            byte[] fresh = RandomNumberGenerator.GetBytes(32);
            stored = Convert.ToBase64String(fresh);
            Run(() => SecureStorage.Default.SetAsync(KeyName, stored).ContinueWith(_ => (string?)null));
        }
        _key = Convert.FromBase64String(stored);
        return _key;
    }

    public byte[] Encrypt(byte[] plain)
    {
        byte[] nonce = RandomNumberGenerator.GetBytes(12);
        byte[] cipher = new byte[plain.Length];
        byte[] tag = new byte[16];
        using var aes = new AesGcm(Key(), 16);
        aes.Encrypt(nonce, plain, cipher, tag);

        byte[] blob = new byte[12 + 16 + cipher.Length];
        Buffer.BlockCopy(nonce, 0, blob, 0, 12);
        Buffer.BlockCopy(tag, 0, blob, 12, 16);
        Buffer.BlockCopy(cipher, 0, blob, 28, cipher.Length);
        return blob;
    }

    public byte[] Decrypt(byte[] blob)
    {
        byte[] nonce = blob[..12];
        byte[] tag = blob[12..28];
        byte[] cipher = blob[28..];
        byte[] plain = new byte[cipher.Length];
        using var aes = new AesGcm(Key(), 16);
        aes.Decrypt(nonce, cipher, tag, plain);   // lève une exception si le fichier a été modifié
        return plain;
    }

    // ---------- Session ----------
    public string? GetSession() => Run(() => SecureStorage.Default.GetAsync(SessionName));

    public void SetSession(string? email)
    {
        if (email == null) SecureStorage.Default.Remove(SessionName);
        else Run(() => SecureStorage.Default.SetAsync(SessionName, email).ContinueWith(_ => (string?)null));
    }

    private static string? Run(Func<Task<string?>> f)
    {
        try { return Task.Run(f).GetAwaiter().GetResult(); }
        catch { return null; }
    }
}
