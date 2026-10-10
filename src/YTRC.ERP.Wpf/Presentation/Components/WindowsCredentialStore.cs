using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;

namespace YTRC.ERP.Wpf;

internal static class WindowsCredentialStore
{
    private const string TargetName = "YTRC-MRP/RememberedPassword";
    private const uint GenericCredential = 1;
    private const uint PersistLocalMachine = 2;
    private const int ErrorNotFound = 1168;
    private const int MaximumCredentialBlobSize = 512;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
        public FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
        [MarshalAs(UnmanagedType.LPWStr)] public string? UserName;
    }

    [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredWrite(ref NativeCredential userCredential, uint flags);

    [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredRead(string targetName, uint type, uint flags, out IntPtr credential);

    [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredDelete(string targetName, uint type, uint flags);

    [DllImport("Advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    public static bool TryReadPassword(string username, out string password)
    {
        password = "";
        if (!CredRead(TargetName, GenericCredential, 0, out var credentialPointer))
            return false;

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(credentialPointer);
            if (!string.IsNullOrWhiteSpace(username) &&
                !string.Equals(credential.UserName, username, StringComparison.OrdinalIgnoreCase))
                return false;
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0 ||
                credential.CredentialBlobSize > MaximumCredentialBlobSize || credential.CredentialBlobSize % 2 != 0)
                return false;

            password = Marshal.PtrToStringUni(
                credential.CredentialBlob, checked((int)credential.CredentialBlobSize / 2)) ?? "";
            return password.Length > 0;
        }
        finally
        {
            CredFree(credentialPointer);
        }
    }

    public static bool TrySavePassword(string username, string password)
    {
        var secret = Encoding.Unicode.GetBytes(password);
        if (secret.Length == 0 || secret.Length > MaximumCredentialBlobSize)
        {
            CryptographicOperations.ZeroMemory(secret);
            return false;
        }

        var blob = Marshal.AllocHGlobal(secret.Length);
        try
        {
            Marshal.Copy(secret, 0, blob, secret.Length);
            var credential = new NativeCredential
            {
                Type = GenericCredential,
                TargetName = TargetName,
                CredentialBlobSize = (uint)secret.Length,
                CredentialBlob = blob,
                Persist = PersistLocalMachine,
                UserName = username
            };
            return CredWrite(ref credential, 0);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
            Marshal.Copy(new byte[secret.Length], 0, blob, secret.Length);
            Marshal.FreeHGlobal(blob);
        }
    }

    public static bool DeletePassword()
    {
        if (CredDelete(TargetName, GenericCredential, 0)) return true;
        return Marshal.GetLastWin32Error() == ErrorNotFound;
    }
}
