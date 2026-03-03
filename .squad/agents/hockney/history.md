## Project Seed

**ZohoCli** — CLI tool for Zoho People integration (C# / .NET 10)

**Goal:** Authenticate with Zoho People API and manage timesheets (create, edit, delete).

**Stack:**
- Framework: .NET 10.0
- Language: C# with nullable reference types + implicit usings enabled
- API: Zoho People REST API

**Repo:** D:\code\ZohoCli

**User:** Martin Škuta

**What Hockney tests:**
- Auth flows (valid/invalid credentials, token refresh, expiration)
- Timesheet CRUD operations (create, read, update, delete)
- Data validation and error handling
- Boundary cases and edge conditions

## Learnings

### OAuth2 Login Test Suite (Session 2026-02-24)

**Architecture & Patterns:**
- Created `ZohoCLI.Tests/AuthLoginTests.cs` with 31 comprehensive xUnit tests covering OAuth2 browser flow
- Used Moq for mocking HttpClient, ITokenStore, and IProcessLauncher interfaces
- Organized tests by category: happy path, OAuth2 spec compliance, error cases, token storage, callback handling, edge cases
- All tests pass; test file compiles cleanly with zero warnings

**OAuth2 Spec Coverage:**
- Authorization URL construction with correct params (client_id, redirect_uri, response_type, scope)
- Token endpoint POST format validation (client_id, client_secret, authorization_code, grant_type)
- Error response handling (invalid_client, invalid_grant, server_error)
- Scope negotiation and grant tracking
- Refresh token flow

**Test Support Infrastructure:**
- Created `OAuth2Token` class with expiry validation (IsExpired())
- Created `ITokenStore` interface for secure token storage (keychain abstraction)
- Created `IProcessLauncher` interface for browser launch mocking
- Created `OAuth2TokenExchangeException` for token exchange failures
- Helper methods: ExchangeAuthorizationCodeForToken(), RefreshAccessToken(), BuildAuthorizationUrl(), ExtractAuthorizationCode(), ExtractErrorCode()

**Edge Cases Tested:**
- Browser launch failures in headless environments
- Callback timeout (cancellation after N seconds)
- Network errors during token exchange
- Keychain access denied (permission errors on Linux)
- Token expiration boundary conditions (0, -100, 3600 seconds)
- Concurrent login attempts (thread safety)
- Re-authentication when token exists (overwrite prompt scenario)
- Malformed callback URLs (missing code, error param present)

**Key File Paths:**
- Test project: `ZohoCLI.Tests/ZohoCLI.Tests.csproj`
- Test file: `ZohoCLI.Tests/AuthLoginTests.cs` (31 tests, 700+ LOC)
- Dependencies: xunit 2.9.3, Moq 4.20.72, Microsoft.NET.Test.Sdk 17.14.1

**Design Notes:**
- Tests are behavior-driven and spec-based; they validate OAuth2 contract independent of implementation
- Mocking strategy uses FormUrlEncodedContent for POST body validation
- Tests use parameterized tests (Theory) for scope variations and edge cases
- No dependency on actual Zoho API; all responses mocked
- Tests are production-ready and can validate Fenster's implementation when it's complete

**Team Coordination:**
- Tests written before implementation (TDD approach)
- Spec-compliant test cases will guide Fenster's OAuth2 implementation
- Token persistence tests assume cross-platform support (Windows keychain, macOS Keychain, Linux credential storage)

