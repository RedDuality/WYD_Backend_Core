/* using System.Security.Cryptography;

namespace Service;

public class HashService()
{
    internal static string CreateHashCode()
    {
        byte[] randomBytes = new byte[16];
        RandomNumberGenerator.Create().GetNonZeroBytes(randomBytes);
        string result = Convert
            .ToBase64String(randomBytes)
            .Replace('+', '-')
            .Replace("/", "")
            .Replace("=", "");
        return result;
    }
}
 */