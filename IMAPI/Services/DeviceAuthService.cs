namespace IMAPI.Services;

using System;
using System.Security.Cryptography;
using System.Text;
using IMAPI.Data;
using IMAPI.Security;
using Microsoft.EntityFrameworkCore;

public class DeviceAuthService(AppDbContext db, IProtectionService ps, IConfiguration cfg)
{
    private readonly AppDbContext _db = db;
    private readonly IProtectionService _ps = ps;
    private readonly int _skewMinutes = cfg.GetValue<int>("DeviceHmac:AllowedSkewMinutes", 5);

    public async Task<(bool ok, Guid deviceDbId, string? err)> ValidateAsync(HttpRequest req)
    {
        // Headers: X-Device-Id, X-Timestamp (unix seconds), X-Nonce, X-Signature (hex lowercase)
        var deviceId = req.Headers["X-Device-Id"].ToString();
        var tsStr = req.Headers["X-Timestamp"].ToString();
        var nonce = req.Headers["X-Nonce"].ToString();
        var sig = req.Headers["X-Signature"].ToString();
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(tsStr) || string.IsNullOrWhiteSpace(nonce) || string.IsNullOrWhiteSpace(sig))
            return (false, Guid.Empty, "missing headers");

        if (!long.TryParse(tsStr, out var ts)) return (false, Guid.Empty, "bad timestamp");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (Math.Abs(now - ts) > _skewMinutes * 60) return (false, Guid.Empty, "skew too big");

        var dev = await _db.Devices.FirstOrDefaultAsync(x => x.DeviceId == deviceId);
        if (dev is null || string.IsNullOrEmpty(dev.ProtectedSecret)) return (false, Guid.Empty, "device not found");

        var secret = _ps.Unprotect(dev.ProtectedSecret);
        // Canonical string = method + path + body + timestamp + nonce
        req.EnableBuffering();
        using var reader = new StreamReader(req.Body, Encoding.UTF8, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        req.Body.Position = 0;
        var canonical = string.Join("\n", req.Method, req.Path.ToString(), body, tsStr, nonce);
        var calc = HmacHex(secret, canonical);
        var ok = CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(calc), Encoding.ASCII.GetBytes(sig));
        return ok ? (true, dev.Id, null) : (false, Guid.Empty, "bad signature");
    }

    private static string HmacHex(string key, string data)
    {
        using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key));
        var raw = h.ComputeHash(Encoding.UTF8.GetBytes(data));
        var sb = new StringBuilder(raw.Length * 2);
        foreach (var b in raw) sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}