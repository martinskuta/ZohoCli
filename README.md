# zoho

A small command line tool that reads Zoho People data as JSON: the signed-in person, their holidays and leave, and the timesheet jobs. It exists for the timesheet workflows of CodeAlta, which call it and read its output.

## Install

`zoho` is a .NET global tool: it needs the .NET 10 SDK, and installing it puts `zoho` in `~/.dotnet/tools`, which is on `PATH`.

**From a release:** download `Zoho.Cli.<version>.nupkg` from the [releases](https://github.com/martinskuta/ZohoCli/releases) into a folder of its own, such as `~/Downloads/zoho`, then:

```powershell
dotnet tool install -g Zoho.Cli --source ~/Downloads/zoho
zoho login
```

**From the source:**

```powershell
dotnet pack src/Zoho.Cli -c Release -o artifacts
dotnet tool install -g Zoho.Cli --source artifacts
zoho login
```

Use `--source`, not `--add-source`: `--add-source` is refused when NuGet package source mapping is configured.

**Update** to a newer package the same way, with `update` in place of `install`:

```powershell
dotnet tool update -g Zoho.Cli --source ~/Downloads/zoho
```

## Uninstall

```powershell
dotnet tool uninstall -g Zoho.Cli
```

That removes the program. Your sign-in and the jobs cache stay, shared with timeshiit; remove them too only if neither tool should stay signed in:

```powershell
Remove-Item -Recurse "$env:APPDATA\ZohoCLI"     # token.json and jobs.json
cmdkey /delete:ZohoCLI:zoho_oauth_token          # the Windows credential
```

## Commands

stdout holds JSON and nothing else; messages go to stderr. The exit code is `0` for success, `1` for an error and `2` when you are not signed in (no token, or Zoho refused it): run `zoho login`.

| Command | Prints |
| --- | --- |
| `zoho login` | Signs in in the browser, then prints `{ "email", "employeeId" }` |
| `zoho me` | `{ "email": "…", "employeeId": "INN-…" }` |
| `zoho leaves --from 2026-11-01 --to 2026-11-30` | `[{ "date": "2026-11-11", "hours": 8, "type": "Holiday", "reason": "…" }]`: holidays and leave in the range; a half day is 4 hours |
| `zoho jobs [--refresh]` | `[{ "clientId", "clientName", "projectId", "projectName", "taskCode", "jobName" }]` |

`zoho jobs` takes about 30 seconds the first time and is then cached for a month in `%APPDATA%\ZohoCLI\jobs.json`; `--refresh` fetches the jobs again.

The JSON is UTF-8 when the console's code page is UTF-8. From PowerShell, set it first so that names with accents survive:

```powershell
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)
zoho jobs | ConvertFrom-Json
```

## Sign-in

`zoho login` opens Zoho's sign-in page (OAuth with PKCE and a public client ID; no secret is stored) and listens on `http://localhost:8080/callback`. The token is kept in the Windows credential `ZohoCLI:zoho_oauth_token` and in `%APPDATA%\ZohoCLI\token.json`, the places timeshiit uses, so both tools share one sign-in. `ZOHO_CLI_DOMAIN` picks the Zoho data center (default `zoho.eu`).

## Develop

```powershell
dotnet build
dotnet test
```

## Build and release

The GitHub Actions workflow [`release.yml`](.github/workflows/release.yml) runs on every push and pull request: it tests, packs `Zoho.Cli.<version>.nupkg`, installs it to check that `zoho` runs (and exits 2 with no sign-in), and uploads the package as an artifact of the run. The version is `<Major.Minor>.<patch>`: `.release-version` holds `Major.Minor`, and the patch is one more than the highest matching `v` tag.

To publish a release, run the workflow by hand on `master` (**Actions > Build and release > Run workflow**, or `gh workflow run release.yml --ref master`): it creates the GitHub release `v<version>` with the package attached.