using System.Security.Cryptography;

namespace MineDeck.Security;

public static class PasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltLength = 16;
    private const int KeyLength = 32;
    private const string Prefix = "pbkdf2-sha256";

    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        var salt = RandomNumberGenerator.GetBytes(SaltLength);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyLength);
        return $"{Prefix}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public static bool Verify(string password, string encoded)
    {
        if (!TryParse(encoded, out var iterations, out var salt, out var expected)) return false;
        var actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expected.Length);
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }

    public static bool IsValidEncoding(string encoded) => TryParse(encoded, out _, out _, out _);

    private static bool TryParse(string encoded, out int iterations, out byte[] salt, out byte[] key)
    {
        iterations = 0;
        salt = Array.Empty<byte>();
        key = Array.Empty<byte>();
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out iterations)) return false;
            salt = Convert.FromBase64String(parts[2]);
            key = Convert.FromBase64String(parts[3]);
            return iterations >= 100_000 && salt.Length >= SaltLength && key.Length >= KeyLength;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
