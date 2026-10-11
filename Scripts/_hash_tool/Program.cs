using Microsoft.AspNetCore.Identity;

internal sealed class HashUser { }

internal static class Program
{
    private static void Main(string[] args)
    {
        var pwd = args.Length > 0 ? args[0] : "12345678";
        var hash = new PasswordHasher<HashUser>().HashPassword(new HashUser(), pwd);
        Console.WriteLine(hash);
    }
}
