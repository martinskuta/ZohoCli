## Project Seed

**ZohoCli** — CLI tool for Zoho People integration (C# / .NET 10)

**Goal:** Authenticate with Zoho People API and manage timesheets (create, edit, delete).

**Stack:**
- Framework: .NET 10.0
- Language: C# with nullable reference types + implicit usings enabled
- API: Zoho People REST API (https://www.zoho.com/projects/help/rest-api/zohoprojectsapi.html)

**User:** Martin Škuta

**Early decisions:**
- Start with architecture / auth design (Keaton lead)
- Build core auth + timesheet models (Fenster)
- Test coverage from day 1 (Hockney)

## Learnings

### Session 1: Auth Architecture Design (2026-02-24)

**Zoho API Patterns Discovered:**
- OAuth 2.0 recommended (not API Key)
- Scopes: `ZohoProjects.timesheets.ALL` for least privilege
- Region-aware endpoints (.com, .eu, .in, .cn)
- Rate limit: 100 calls per 2 minutes per user

**Architecture Decisions Made:**
- Auth flow: OAuth 2.0 with local token persistence (~/.zoho_cli/credentials.json)
- Token refresh: Auto-refresh if expiry within 5 minutes (transparent to caller)
- Storage: DPAPI on Windows for MVP; keychain later
- CLI: System.CommandLine (four subcommands: login, logout, status, refresh)

**Approved Command Structure:**
- `zoho auth login [--region com|eu|in|cn]`
- `zoho auth logout [--force]`
- `zoho auth status [--verbose]`
- `zoho auth refresh [--force]`

**Module Structure (for Fenster):**
- Auth/ (OAuth2Client, TokenStore, AuthService, IAuthProvider)
- Api/ (ZohoApiClient with auto-refresh, IZohoApiClient)
- Timesheet/ (TimesheetService, ITimesheetService)
- Common/ (Exceptions, Constants, AppConfig)
- DI via Microsoft.Extensions.DependencyInjection

**Blocking Decisions (awaiting Martin approval):**
1. OAuth credentials source (recommending env vars: ZOHO_CLIENT_ID, ZOHO_CLIENT_SECRET)
2. Token encryption level (recommend DPAPI for MVP)
3. Timesheet data model (which fields: user_id, project_id, date, hours, notes?)

**Key Deliverable:**
- File: `.squad/decisions/inbox/keaton-auth-architecture.md` (detailed proposal, waiting for Martin sign-off)
