using System.Runtime.InteropServices;
using System.Security;
using System.Security.Cryptography;

namespace AiGuardLock;

/// <summary>Short-lived, zeroed copy of a PasswordBox SecurePassword.</summary>
internal sealed class PasswordBuffer : IDisposable
{
    private readonly char[] _characters;
    public ReadOnlySpan<char> Characters => _characters;

    public PasswordBuffer(SecureString password)
    {
        _characters = new char[password.Length];
        IntPtr pointer = Marshal.SecureStringToGlobalAllocUnicode(password);
        try { Marshal.Copy(pointer, _characters, 0, _characters.Length); }
        finally { Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
    }

    public void Dispose() => CryptographicOperations.ZeroMemory(MemoryMarshal.AsBytes(_characters.AsSpan()));
}
