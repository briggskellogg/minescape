using MineDeck.Security;
using System.Security.Cryptography;
using System.Text.Json;

if (args.Length == 1 && args[0].Equals("--generate-json", StringComparison.OrdinalIgnoreCase))
{
    var generatedPassword = Convert.ToBase64String(RandomNumberGenerator.GetBytes(18))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    Console.WriteLine(JsonSerializer.Serialize(new { password = generatedPassword, hash = PasswordHasher.Hash(generatedPassword) }));
    return 0;
}

Console.Write("New MineDeck administrator password: ");
var password = ReadSecret();
Console.WriteLine();
Console.Write("Repeat password: ");
var confirmation = ReadSecret();
Console.WriteLine();

if (password != confirmation || password.Length < 12)
{
    Console.Error.WriteLine("Passwords must match and contain at least 12 characters.");
    return 1;
}

Console.WriteLine(PasswordHasher.Hash(password));
return 0;

static string ReadSecret()
{
    var value = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) return value.ToString();
        if (key.Key == ConsoleKey.Backspace && value.Length > 0)
        {
            value.Length--;
            continue;
        }
        if (!char.IsControl(key.KeyChar)) value.Append(key.KeyChar);
    }
}
