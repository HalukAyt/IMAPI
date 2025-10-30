using System.Security.Cryptography;
using System.Text;


namespace IMAPI.Api.Services;


public class PasswordHasher
{
    private const int SaltSize = 16; // bytes
    private const int KeySize = 32; // bytes
    private const int Iterations = 100_000;


    public (string Hash, string Salt) HashPassword(string password)
    {
        var saltBytes = RandomNumberGenerator.GetBytes(SaltSize);
        var hashBytes = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);
        return (Convert.ToBase64String(hashBytes), Convert.ToBase64String(saltBytes));
    }


    public bool Verify(string password, string storedHash, string storedSalt)
    {
        var saltBytes = Convert.FromBase64String(storedSalt);
        var hashToCompare = Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, Iterations, HashAlgorithmName.SHA256, KeySize);
        return CryptographicOperations.FixedTimeEquals(Convert.FromBase64String(storedHash), hashToCompare);
    }
}