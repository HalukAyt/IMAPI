namespace IMAPI.Security;
using Microsoft.AspNetCore.DataProtection;

public interface IProtectionService
{
    string Protect(string plaintext);
    string Unprotect(string cipher);
}

public class ProtectionService(IDataProtector protector) : IProtectionService
{
    private readonly IDataProtector _p = protector;
    public string Protect(string plaintext) => _p.Protect(plaintext);
    public string Unprotect(string cipher) => _p.Unprotect(cipher);
}