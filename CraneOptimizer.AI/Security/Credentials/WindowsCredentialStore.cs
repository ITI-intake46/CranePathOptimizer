using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace CraneOptimizer.AI.Security.Credentials
{
    /// <summary>
    /// Stores BYOK provider secrets in Windows Credential Manager. The settings JSON contains
    /// only the credential target name; the secret is never serialized, logged, or exported.
    /// </summary>
    public sealed class WindowsCredentialStore : IAiCredentialStore
    {
        private const uint CredTypeGeneric = 1;
        private const uint CredPersistLocalMachine = 2;

        public bool Contains(string targetName)
        {
            ValidateTarget(targetName);
            if (CredRead(targetName, CredTypeGeneric, 0, out var credentialPtr))
            {
                CredFree(credentialPtr);
                return true;
            }

            var error = Marshal.GetLastWin32Error();
            if (error == 1168)
                return false;
            throw new Win32Exception(error, "Windows Credential Manager could not inspect the AI provider credential.");
        }

        public string? Read(string targetName)
        {
            ValidateTarget(targetName);
            if (!CredRead(targetName, CredTypeGeneric, 0, out var credentialPtr))
            {
                var error = Marshal.GetLastWin32Error();
                if (error == 1168) // ERROR_NOT_FOUND
                    return null;
                throw new Win32Exception(error, "Windows Credential Manager could not read the AI provider credential.");
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(credentialPtr);
                if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
                    return string.Empty;

                var bytes = new byte[checked((int)credential.CredentialBlobSize)];
                try
                {
                    Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
                    return Encoding.UTF8.GetString(bytes);
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(bytes);
                }
            }
            finally
            {
                CredFree(credentialPtr);
            }
        }

        public void Save(string targetName, string secret)
        {
            ValidateTarget(targetName);
            if (string.IsNullOrWhiteSpace(secret))
                throw new ArgumentException("The API key cannot be empty.", nameof(secret));

            var bytes = Encoding.UTF8.GetBytes(secret.Trim());
            if (bytes.Length > 2_560)
                throw new ArgumentException("The API key exceeds the Windows Credential Manager size limit.", nameof(secret));
            var blobPtr = Marshal.AllocCoTaskMem(bytes.Length);
            try
            {
                Marshal.Copy(bytes, 0, blobPtr, bytes.Length);
                var credential = new NativeCredential
                {
                    Type = CredTypeGeneric,
                    TargetName = targetName,
                    CredentialBlobSize = (uint)bytes.Length,
                    CredentialBlob = blobPtr,
                    Persist = CredPersistLocalMachine,
                    UserName = Environment.UserName
                };

                if (!CredWrite(ref credential, 0))
                    throw new Win32Exception(Marshal.GetLastWin32Error(), "Windows Credential Manager could not store the AI provider credential.");
            }
            finally
            {
                CryptographicOperations.ZeroMemory(bytes);
                Marshal.FreeCoTaskMem(blobPtr);
            }
        }

        public void Delete(string targetName)
        {
            ValidateTarget(targetName);
            if (CredDelete(targetName, CredTypeGeneric, 0))
                return;

            var error = Marshal.GetLastWin32Error();
            if (error != 1168)
                throw new Win32Exception(error, "Windows Credential Manager could not remove the AI provider credential.");
        }

        private static void ValidateTarget(string targetName)
        {
            if (!OperatingSystem.IsWindows())
                throw new PlatformNotSupportedException("AI provider credentials require Windows Credential Manager.");
            if (string.IsNullOrWhiteSpace(targetName))
                throw new ArgumentException("A credential target name is required.", nameof(targetName));
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            [MarshalAs(UnmanagedType.LPWStr)] public string TargetName;
            [MarshalAs(UnmanagedType.LPWStr)] public string? Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            [MarshalAs(UnmanagedType.LPWStr)] public string? TargetAlias;
            [MarshalAs(UnmanagedType.LPWStr)] public string UserName;
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credentialPtr);

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite([In] ref NativeCredential userCredential, uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(string target, uint type, uint flags);

        [DllImport("Advapi32.dll", SetLastError = true)]
        private static extern void CredFree(IntPtr buffer);
    }
}
