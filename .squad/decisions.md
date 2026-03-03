# Squad Decisions

*(append-only — merged from .squad/decisions/inbox/ by Scribe)*

**Session:** 2026-02-24 (Martin)

- **Team formed:** Keaton (Lead), Fenster (Backend), Hockney (Tester), Scribe, Ralph — Heat universe
- **First move:** Keaton to design auth + timesheet architecture
- **OAuth2 header fix:** Zoho API requires `Authorization` header (not body) with format `Zoho-oauthtoken {token}`
- **OAuth choice approved:** OAuth2 (vs API Key) — Zoho's recommended auth method, supports scoping and token expiry
- **Auth flow:** Browser popup + local callback listener (http://localhost:8080)
- **CLI commands:** `zoho auth login`, `zoho auth logout`, `zoho auth status`, `zoho auth refresh`
- **Token storage:** Cross-platform keychain (Windows Credential Manager, macOS Keychain, Linux Secret Service; JSON fallback)
- **OAuth response_type bug fixed:** Ensured authorization URL includes `response_type=code` for OAuth2 authorization code flow

**Session:** 2026-02-25 (Martin, Fenster work)

- **Command structure:** Manual CLI parsing → System.CommandLine 2.0.0-beta4 refactor for proper subcommands and help generation
- **OAuth2 MVP implementation:** `OAuthService.cs` + `TokenStore.cs` + `AuthLoginCommand.cs` — full flow working
- **Test architecture (Hockney):** 31 xUnit test cases for OAuth2 flow, PKCE compliance, error scenarios, token storage
- **PKCE refactor:** Removed `ClientSecret`, implemented code_verifier/code_challenge generation (SHA256/Base64Url), updated token exchange/refresh
- **CLI structure finalized:** `auth {login|logout|status}`, `jobs get`, `leave get all` subcommands
- **Next phase:** Timesheet CRUD commands, response formatting, integration tests
