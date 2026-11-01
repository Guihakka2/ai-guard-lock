using System.Security.Cryptography;

namespace AiGuardLock.Core;

/// <summary>Session-only credential; no password or hash is written to disk.</summary>
public sealed class PasswordCredential : IDisposable
{
    public const int MinimumLength = 8;
    public const int MaximumLength = 128;
    private const int Iterations = 210_000;
    private readonly byte[] _salt = RandomNumberGenerator.GetBytes(16);
    private readonly byte[] _hash;
    private bool _disposed;

    public PasswordCredential(ReadOnlySpan<char> password)
    {
        if (!IsValidInput(password))
            throw new ArgumentException("数字だけのPINは4〜12桁、パスワードは空白だけを避けて8〜128文字で設定してください。", nameof(password));

        _hash = Rfc2898DeriveBytes.Pbkdf2(password, _salt, Iterations, HashAlgorithmName.SHA256, 32);
    }

    public bool Verify(ReadOnlySpan<char> password)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (!IsValidInput(password))
            return false;

        Span<byte> candidate = stackalloc byte[32];
        try
        {
            Rfc2898DeriveBytes.Pbkdf2(password, _salt, candidate, Iterations, HashAlgorithmName.SHA256);
            return CryptographicOperations.FixedTimeEquals(_hash, candidate);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(candidate);
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        CryptographicOperations.ZeroMemory(_hash);
        CryptographicOperations.ZeroMemory(_salt);
        _disposed = true;
    }

    private static bool IsValidInput(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value.Length > MaximumLength || value.IsWhiteSpace()) return false;
        var digitsOnly = true;
        foreach (var character in value)
            if (character < '0' || character > '9') { digitsOnly = false; break; }
        return digitsOnly ? value.Length is >= 4 and <= 12 : value.Length >= MinimumLength;
    }
}
