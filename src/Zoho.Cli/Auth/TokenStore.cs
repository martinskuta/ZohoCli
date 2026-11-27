using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Text.Json;
using Zoho.Cli.Zoho;

namespace Zoho.Cli.Auth;

internal interface ITokenStore
{
    Task<OAuthToken?> LoadAsync();
    Task SaveAsync(OAuthToken token);
}

/// <summary>
/// Keeps the token in the Windows credential "ZohoCLI:zoho_oauth_token" and in %APPDATA%\ZohoCLI\token.json,
/// the places timeshiit uses, so one sign-in serves both tools. The file is the fallback when the credential
/// cannot be read, and the only store on other platforms.
/// </summary>
internal sealed class TokenStore(ZohoSettings settings) : ITokenStore
{
    private const string CredentialTarget = "ZohoCLI:zoho_oauth_token";
    private string FilePath => Path.Combine(settings.ConfigDirectory, "token.json");

    public async Task<OAuthToken?> LoadAsync()
    {
        var json = OperatingSystem.IsWindows() ? CredentialManager.Read(CredentialTarget) : null;
        if (json == null && File.Exists(FilePath)) json = await File.ReadAllTextAsync(FilePath);
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize(json, ZohoJson.Default.OAuthToken);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public async Task SaveAsync(OAuthToken token)
    {
        var json = JsonSerializer.Serialize(token, ZohoJson.Default.OAuthToken);
        if (OperatingSystem.IsWindows()) CredentialManager.Write(CredentialTarget, json);

        Directory.CreateDirectory(settings.ConfigDirectory);
        await File.WriteAllTextAsync(FilePath, json);
    }

    private static class CredentialManager
    {
        private const uint GenericType = 1;
        private const uint PersistEnterprise = 3;

        public static string? Read(string target)
        {
            if (!CredRead(target, GenericType, 0, out var pointer)) return null;
            try
            {
                var credential = Marshal.PtrToStructure<NativeCredential>(pointer);
                return Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2);
            }
            finally
            {
                CredFree(pointer);
            }
        }

        public static void Write(string target, string secret)
        {
            var bytes = Encoding.Unicode.GetBytes(secret);
            var credential = new NativeCredential
            {
                Type = GenericType,
                TargetName = target,
                CredentialBlobSize = (uint)bytes.Length,
                CredentialBlob = Marshal.AllocCoTaskMem(bytes.Length),
                Persist = PersistEnterprise,
                UserName = Environment.UserName,
                Comment = "ZohoCLI OAuth Token"
            };

            try
            {
                Marshal.Copy(bytes, 0, credential.CredentialBlob, bytes.Length);
                if (!CredWrite(ref credential, 0))
                    throw new IOException($"Could not save the token in the Windows credential {target} (error {Marshal.GetLastWin32Error()}).");
            }
            finally
            {
                Marshal.FreeCoTaskMem(credential.CredentialBlob);
            }
        }

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredReadW")]
        private static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode, EntryPoint = "CredWriteW")]
        private static extern bool CredWrite(ref NativeCredential credential, uint flags);

        [DllImport("advapi32.dll")]
        private static extern void CredFree(IntPtr credential);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string Comment;
            public FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string TargetAlias;
            public string UserName;
        }
    }
}
