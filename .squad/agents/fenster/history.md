## Project Seed

**ZohoCli** — CLI tool for Zoho People integration (C# / .NET 10)

**Goal:** Authenticate with Zoho People API and manage timesheets (create, edit, delete).

**Stack:**
- Framework: .NET 10.0
- Language: C# with nullable reference types + implicit usings enabled
- API: Zoho People REST API

**Repo:** D:\code\ZohoCli

**User:** Martin Škuta

**What Fenster builds:**
- OAuth2 / API key auth with Zoho People
- Timesheet CRUD models and operations
- API client wrapping Zoho endpoints
- CLI commands exposing timesheet operations

## Learnings

### OAuth2 Implementation (Session 2026-02-24)

**Files Created:**
- `ZohoCLI/src/Auth/OAuthService.cs` — Handles browser-based OAuth2 flow:
  - Launches system default browser
  - Implements HTTP callback listener on localhost:8080
  - Exchanges authorization code for access tokens
  - Supports token refresh
  - Cross-platform browser launch (Windows cmd /c start, macOS open, Linux xdg-open)

- `ZohoCLI/src/Auth/TokenStore.cs` — Cross-platform secure token storage:
  - Windows: Uses Windows Credential Manager (advapi32 P/Invoke)
  - macOS: Uses `security` command-line tool
  - Linux: Uses `secret-tool` (Secret Service API)
  - Fallback: Local JSON file in AppData with encryption ready

- `ZohoCLI/src/Commands/AuthLoginCommand.cs` — Auth CLI handler

- `ZohoCLI/Program.cs` — Entry point with manual argument parsing
  - Avoids System.CommandLine version conflicts (.NET 10 had compatibility issues)
  - Clean argv parsing for `auth login --client-id=X --client-secret=Y`
  - Reads from env vars (ZOHO_CLIENT_ID, ZOHO_CLIENT_SECRET) as fallback

**Key Decisions:**
- Used manual CLI parsing instead of System.CommandLine due to .NET 10 compatibility issues with beta versions
- OAuth2 flow uses HTTP listener on localhost:8080 (no external deps)
- Token expiry tracking via DateTimeOffset (enables auto-refresh on next use)
- Secure token storage with platform-specific keychain integration

**Zoho OAuth2 Endpoints:**
- Auth: https://accounts.zoho.com/oauth/v2/auth
- Token: https://accounts.zoho.com/oauth/v2/token
- Scope: ZohoProjects.timesheets.ALL
- Redirect: http://localhost:8080/callback

**Build Status:** ✓ Compiles without warnings, Release + Debug modes work

### OAuth2 Response Type Bug Investigation (Session 2026-02-24 — Martin)

**Issue:** Reported "Invalid Response Type" error from Zoho — missing or invalid `response_type` parameter.

**Investigation:**
- Reviewed `ZohoCLI/src/Auth/OAuthService.cs`
- Found `BuildAuthorizationUrl()` method (lines 44–59)
- **Result: ✅ NO BUG FOUND** — Implementation already correct:
  - `response_type` is set to `"code"` (line 49)
  - Parameter is properly URL-encoded using `Uri.EscapeDataString()`
  - All required OAuth2 params present: `client_id`, `redirect_uri`, `scope`, `response_type`, `access_type`
  - Tests explicitly verify `response_type=code` is present (AuthLoginTests.cs:124)
  - All tests pass

**Resolution:**
Code was already correct. Authorization URL will properly send:
```
https://accounts.zoho.com/oauth/v2/auth?
  client_id=...&
  response_type=code&     ← Correct for authorization code flow
  scope=...&
  redirect_uri=...&
  access_type=offline
```

**Next Steps:** Token refresh logic, API client wrapper, timesheet models

### CLI Refactor to System.CommandLine (Session 2026-02-24)

**Goal:** Replace manual CLI argument parsing with System.CommandLine for proper command tree structure.

**Changes Made:**
- **Program.cs** — Refactored to use System.CommandLine 2.0.0-beta4.22272.1:
  - Created command tree: `auth` command group → `auth login` subcommand
  - Added `--client-id` and `--client-secret` options with env var fallbacks (ZOHO_CLIENT_ID, ZOHO_CLIENT_SECRET)
  - Used `ICommandHandler` interface with custom `AuthLoginHandler` class for proper async handling
  - Returns proper exit codes (0 for success, 1 for errors)

- **AuthLoginCommand.cs** — Updated signature:
  - `HandleLoginAsync()` now accepts `clientId` and `clientSecret` parameters
  - No breaking changes to internal logic — only wiring changes

- **OAuthService.cs** — Constructor injection for credentials:
  - Removed hardcoded `ClientId` and `ClientSecret` constants
  - Added constructor: `OAuthService(string clientId, string clientSecret)`
  - Updated all internal references to use `_clientId` and `_clientSecret` fields
  - No changes to OAuth2 flow logic

**System.CommandLine Patterns Learned:**
- **Version 2.0.0-beta4** uses `ICommandHandler` interface (not `SetHandler` or `CommandHandler.Create`)
- Must implement both `Invoke()` and `InvokeAsync()` methods
- Options bind to handler properties via naming convention (`ClientId` → `--client-id`)
- Command hierarchy built with `.Add()` method on `Command` and `RootCommand`
- `RootCommand.InvokeAsync(args)` executes the command tree

**Expected Usage:**
```bash
zoho auth login --client-id=1000.XXX --client-secret=YYY
# OR with env vars:
export ZOHO_CLIENT_ID=1000.XXX
export ZOHO_CLIENT_SECRET=YYY
zoho auth login
```

**Build Status:** ✓ Compiles with 0 warnings, help text generates correctly

**Next Steps:** Token refresh logic, API client wrapper, timesheet models

### GetUserInfoAsync 400 Error Fix (Session 2026-02-24)

**Bug Reported:** OAuthService.GetUserInfoAsync was returning 400 Bad Request from Zoho API.

**Root Cause:** Authorization was incorrectly sent as form-encoded POST body data instead of as an HTTP header.

**Original Code (WRONG):**
```csharp
var contentParams = new Dictionary<string, string> {
    { "Authorization", "Zoho-oauthtoken " + token.AccessToken }
};
using var content = new FormUrlEncodedContent(contentParams);
var response = await _httpClient.PostAsync("https://accounts.zoho.eu/oauth/user/info", content);
```

**Fixed Code (CORRECT):**
```csharp
using var request = new HttpRequestMessage(HttpMethod.Get, "https://accounts.zoho.eu/oauth/user/info");
request.Headers.Add("Authorization", "Zoho-oauthtoken " + token.AccessToken);
var response = await _httpClient.SendAsync(request, cancellationToken);
```

**Key Changes:**
1. Changed from POST to GET request
2. Moved Authorization from form body to HTTP headers
3. Added proper error handling with detailed exception messages
4. Method now returns `UserInfo` object instead of void
5. Created `UserInfo` model class with JSON property mappings

**Zoho API Requirements for User Info Endpoint:**
- **URL:** https://accounts.zoho.eu/oauth/user/info
- **Method:** GET (not POST)
- **Headers:** `Authorization: Zoho-oauthtoken {access_token}`
- **Response:** JSON with Email, First_Name, Last_Name, ZUID, Display_Name

**Lesson Learned:** Always send OAuth bearer tokens in Authorization header, never in request body. Zoho uses custom format `Zoho-oauthtoken {token}` instead of standard `Bearer {token}`.

**Build Status:** ✓ Compiles with 0 warnings

### Refactor: Command Factory & Base Classes (Session 2026-02-25 — Martin)

**Goal:** Reduce code duplication across auth, jobs, and leave commands. Establish patterns for authenticated API calls.

**Changes Made:**

1. **CommandFactory.cs** — Central command instantiation
   - Lazy singleton instances for HttpClient, TokenStore, OAuthService
   - Factory methods for all commands
   - Ensures consistent dependency injection across CLI

2. **CommandBase.cs** — Base class for all commands
   - Abstract template for command execution pattern
   - Interface: `Execute()` delegates to subclass `ExecuteAsync(CancellationToken)`

3. **AuthenticatedCommand.cs** — Base class for OAuth-protected commands
   - Extends CommandBase
   - Manages OAuth token lifecycle (refresh on expiry)
   - Provides `SendAuthenticatedAsync()` helper (adds Zoho-oauthtoken header)
   - Provides `GetUserEmailAsync()` helper (caches user info call)

4. **Program.cs** — Refactored command routing
   - Uses CommandFactory to instantiate commands
   - Proper command tree structure: `auth`, `jobs`, `leave` as command groups
   - Subcommands: `auth login|logout|status`, `jobs get`, `leave get all`
   - Handler wiring via `SetHandler()` with lambda delegates

5. **New Commands Added:**
   - `Commands/Auth/AuthStatusCommand.cs` — Check if user has valid token
   - `Commands/Auth/AuthLoginCommand.cs` — OAuth2 login flow (refactored)
   - `Commands/Auth/AuthLogoutCommand.cs` — Revoke token and clear storage
   - `Commands/Jobs/JobsGetCommand.cs` — GET /api/timetracker/getjobs
   - `Commands/Leave/LeaveGetAll.cs` — GET /api/leave/v2/holidays + /api/v2/leavetracker/leaves

**Architecture Benefits:**
- No code duplication for OAuth header handling, token refresh, error responses
- Easy to add new commands — just extend AuthenticatedCommand and wire in Program.cs
- Factory encapsulates dependency resolution
- CommandBase template ensures consistent async/error handling

**Key Files Structure:**
```
ZohoCLI/
├── Auth/
│   ├── OAuthService.cs
│   └── TokenStore.cs
├── Commands/
│   ├── CommandBase.cs
│   ├── CommandFactory.cs
│   ├── AuthenticatedCommand.cs
│   ├── Auth/
│   │   ├── AuthStatusCommand.cs
│   │   ├── AuthLoginCommand.cs
│   │   └── AuthLogoutCommand.cs
│   ├── Jobs/
│   │   └── JobsGetCommand.cs
│   └── Leave/
│       └── LeaveGetAll.cs
├── Program.cs
└── UriFormatter.cs
```

**URI Formatting Utility (UriFormatter.cs):**
- `FormatString()` — URL-encode strings (Zoho API compatibility)
- `FormatDate()` — Convert DateOnly to Zoho date format (dd/MM/yyyy)
- `FormattedDefaultDateFormat` — Date format constant for API queries

**Next Steps:**
- Implement timesheet CRUD (create, update, delete)
- Add parsing/output formatting for API responses (JSON → human-readable tables)
- Integration tests for command factory + routing
- Error handling edge cases (token expiry mid-request, network timeouts)

**Build Status:** ✓ Compiles with 0 warnings, all commands wire correctly

### PKCE Flow Refactor (Session 2026-02-25 — Martin)

**Goal:** Replace server-side OAuth2 (client_secret) with PKCE flow, appropriate for CLI public clients.

**Changes to OAuthService.cs:**
- Removed `ClientSecret` constant entirely
- Added `_codeVerifier` instance field to persist PKCE state across the auth flow
- `BuildAuthorizationUrl()`: generates `code_verifier` (32 random bytes → Base64Url), computes `code_challenge` (SHA256 → Base64Url), appends `code_challenge` + `code_challenge_method=S256` to URL; removed `prompt=none`
- `ExchangeCodeForTokenAsync()`: replaced `client_secret` param with `code_verifier`
- `RefreshTokenAsync()`: removed `client_secret` — Zoho PKCE apps don't require it for refresh
- Added `using System.Security.Cryptography` for `RandomNumberGenerator` and `SHA256`

**PKCE encoding pattern:**
```csharp
private static string Base64UrlEncode(byte[] bytes)
    => Convert.ToBase64String(bytes)
        .Replace('+', '-').Replace('/', '_').TrimEnd('=');
```

**Key Lesson:** CLI apps are public clients — `client_secret` in a distributed binary is insecure. PKCE replaces it with a per-session cryptographic challenge (no static secret needed).

**Build Status:** ✓ Compiles with 0 warnings
