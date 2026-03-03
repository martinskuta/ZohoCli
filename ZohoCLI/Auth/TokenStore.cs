using System.Runtime.InteropServices;
using System.Text.Json;

namespace ZohoCLI.Auth;

public class TokenStore
{
    private const string ServiceName = "ZohoCLI";
    private const string AccountName = "zoho_oauth_token";
    private readonly string _tokenCachePath;

    public TokenStore()
    {
        var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var zohoCachePath = Path.Combine(appDataPath, "ZohoCLI");
        Directory.CreateDirectory(zohoCachePath);
        _tokenCachePath = Path.Combine(zohoCachePath, "token.json");
    }

    public async Task<OAuthToken?> GetTokenAsync()
    {
        try
        {
            var credentials = GetCredentialsFromKeychain();
            if (credentials == null)
            {
                return null;
            }

            var token = JsonSerializer.Deserialize<OAuthToken>(credentials, OAuthTokenJsonContext.Default.OAuthToken);
            return token;
        }
        catch
        {
            // Fallback to file cache if keychain fails
            return await GetTokenFromFileAsync();
        }
    }

    public async Task SaveTokenAsync(OAuthToken token)
    {
        try
        {
            var json = JsonSerializer.Serialize(token, OAuthTokenJsonContext.Default.OAuthToken);
            SaveCredentialsToKeychain(json);
            
            // Also save to file as backup
            await SaveTokenToFileAsync(token);
        }
        catch
        {
            // Fallback to file only
            await SaveTokenToFileAsync(token);
        }
    }

    public async Task ClearTokenAsync()
    {
        try
        {
            ClearCredentialsFromKeychain();
        }
        catch
        {
            // Continue even if keychain deletion fails
        }

        try
        {
            if (File.Exists(_tokenCachePath))
            {
                File.Delete(_tokenCachePath);
            }
        }
        catch
        {
            // Ignore file deletion errors
        }

        await Task.CompletedTask;
    }

    private string? GetCredentialsFromKeychain()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return GetWindowsCredentials();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return GetMacOSCredentials();
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return GetLinuxCredentials();
        }

        return null;
    }

    private void SaveCredentialsToKeychain(string credentials)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            SaveWindowsCredentials(credentials);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            SaveMacOSCredentials(credentials);
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            SaveLinuxCredentials(credentials);
        }
    }

    private void ClearCredentialsFromKeychain()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            ClearWindowsCredentials();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            ClearMacOSCredentials();
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            ClearLinuxCredentials();
        }
    }

    #region Windows Credential Manager

    private string? GetWindowsCredentials()
    {
        try
        {
            var credentialBlob = GetCredentialFromVault(ServiceName, AccountName);
            return credentialBlob;
        }
        catch
        {
            return null;
        }
    }

    private void SaveWindowsCredentials(string credentials)
    {
        StoreCredentialInVault(ServiceName, AccountName, credentials);
    }

    private void ClearWindowsCredentials()
    {
        DeleteCredentialFromVault(ServiceName, AccountName);
    }

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredRead(string target, uint type, uint reservedFlag, out IntPtr credential);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CredDelete(string target, uint type, uint flags);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern void CredFree(IntPtr cred);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public uint Flags;
        public uint Type;
        public string TargetName;
        public string Comment;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public string TargetAlias;
        public string UserName;
    }

    private static string? GetCredentialFromVault(string service, string account)
    {
        var targetName = $"{service}:{account}";
        
        if (CredRead(targetName, 1, 0, out IntPtr credPtr))
        {
            var nativeCred = Marshal.PtrToStructure<NativeCredential>(credPtr);
            var credentialBlob = Marshal.PtrToStringUni(nativeCred.CredentialBlob, (int)nativeCred.CredentialBlobSize / 2);
            CredFree(credPtr);
            return credentialBlob;
        }

        return null;
    }

    private static void StoreCredentialInVault(string service, string account, string password)
    {
        var targetName = $"{service}:{account}";
        var passwordBytes = System.Text.Encoding.Unicode.GetBytes(password);
        
        var credential = new NativeCredential
        {
            TargetName = targetName,
            CredentialBlob = Marshal.AllocCoTaskMem(passwordBytes.Length),
            CredentialBlobSize = (uint)passwordBytes.Length,
            Type = 1,
            Persist = 3,
            UserName = Environment.UserName,
            Comment = "ZohoCLI OAuth Token"
        };

        Marshal.Copy(passwordBytes, 0, credential.CredentialBlob, passwordBytes.Length);
        
        try
        {
            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException("Failed to write credential to Windows Credential Manager");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(credential.CredentialBlob);
        }
    }

    private static void DeleteCredentialFromVault(string service, string account)
    {
        var targetName = $"{service}:{account}";
        CredDelete(targetName, 1, 0);
    }

    #endregion

    #region macOS Keychain

    private string? GetMacOSCredentials()
    {
        try
        {
            var result = ExecuteSecurityCommand($"find-generic-password -s {ServiceName} -a {AccountName} -w");
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }
        catch
        {
            return null;
        }
    }

    private void SaveMacOSCredentials(string credentials)
    {
        ExecuteSecurityCommand($"add-generic-password -s {ServiceName} -a {AccountName} -w \"{credentials}\" -U");
    }

    private void ClearMacOSCredentials()
    {
        ExecuteSecurityCommand($"delete-generic-password -s {ServiceName} -a {AccountName}");
    }

    #endregion

    #region Linux Secret Service

    private string? GetLinuxCredentials()
    {
        try
        {
            var result = ExecuteSecretToolCommand($"lookup service ZohoCLI account zoho_oauth_token");
            return string.IsNullOrWhiteSpace(result) ? null : result.Trim();
        }
        catch
        {
            return null;
        }
    }

    private void SaveLinuxCredentials(string credentials)
    {
        ExecuteSecretToolCommand($"store --label='ZohoCLI OAuth Token' service ZohoCLI account zoho_oauth_token", credentials);
    }

    private void ClearLinuxCredentials()
    {
        ExecuteSecretToolCommand($"clear service ZohoCLI account zoho_oauth_token");
    }

    #endregion

    #region Command Execution Helpers

    private static string ExecuteSecurityCommand(string arguments)
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "security",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };

        process.Start();
        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output;
    }

    private static string ExecuteSecretToolCommand(string arguments, string? stdin = null)
    {
        var process = new System.Diagnostics.Process
        {
            StartInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "secret-tool",
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                RedirectStandardInput = stdin != null,
                CreateNoWindow = true
            }
        };

        process.Start();

        if (stdin != null)
        {
            process.StandardInput.WriteLine(stdin);
            process.StandardInput.Close();
        }

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return output;
    }

    #endregion

    #region File Cache Fallback

    private async Task<OAuthToken?> GetTokenFromFileAsync()
    {
        try
        {
            if (!File.Exists(_tokenCachePath))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(_tokenCachePath);
            return JsonSerializer.Deserialize<OAuthToken>(json, OAuthTokenJsonContext.Default.OAuthToken);
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveTokenToFileAsync(OAuthToken token)
    {
        try
        {
            var json = JsonSerializer.Serialize(token, OAuthTokenJsonContext.Default.OAuthToken);
            await File.WriteAllTextAsync(_tokenCachePath, json);
        }
        catch
        {
            // Ignore file write errors
        }
    }

    #endregion
}
