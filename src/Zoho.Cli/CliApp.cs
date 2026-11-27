using System.CommandLine;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Zoho.Cli.Auth;
using Zoho.Cli.Zoho;

namespace Zoho.Cli;

/// <summary>The four commands the timesheet workflows call. stdout holds JSON only; messages go to stderr.</summary>
internal sealed class CliApp(ZohoClient zoho, OAuthClient oauth, ITokenStore tokens, JobsCache jobsCache,
    TextWriter output, TextWriter error)
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int NotSignedIn = 2;

    public static CliApp CreateDefault()
    {
        var settings = ZohoSettings.FromEnvironment();
        var http = new HttpClient();
        var oauth = new OAuthClient(http, settings, TimeProvider.System);
        var tokens = new TokenStore(settings);
        var zoho = new ZohoClient(http, tokens, oauth, settings, TimeProvider.System);
        var cache = new JobsCache(Path.Combine(settings.ConfigDirectory, "jobs.json"), TimeProvider.System);
        return new CliApp(zoho, oauth, tokens, cache, Console.Out, Console.Error);
    }

    public async Task<int> RunAsync(string[] args)
    {
        var root = new RootCommand("Reads Zoho People leaves and timesheet jobs as JSON. Exit codes: 0 success, 1 error, 2 not signed in.");

        var login = new Command("login", "Signs in to Zoho People in the browser and saves the token.");
        login.SetAction((_, ct) => Run(() => LoginAsync(ct)));
        root.Subcommands.Add(login);

        var me = new Command("me", "Prints the signed-in person: email and employee ID.");
        me.SetAction((_, ct) => Run(() => MeAsync(ct)));
        root.Subcommands.Add(me);

        var from = new Option<string>("--from") { Description = "First date, yyyy-MM-dd (inclusive)", Required = true };
        var to = new Option<string>("--to") { Description = "Last date, yyyy-MM-dd (inclusive)", Required = true };
        var leaves = new Command("leaves", "Prints holidays and leave in a date range.") { from, to };
        leaves.SetAction((result, ct) => Run(() =>
            LeavesAsync(ParseDate(result.GetRequiredValue(from), "--from"), ParseDate(result.GetRequiredValue(to), "--to"), ct)));
        root.Subcommands.Add(leaves);

        var refresh = new Option<bool>("--refresh") { Description = "Ignore the cached jobs and fetch them again" };
        var jobs = new Command("jobs", "Prints the timesheet jobs (client, project, task code, job name).") { refresh };
        jobs.SetAction((result, ct) => Run(() => JobsAsync(result.GetValue(refresh), ct)));
        root.Subcommands.Add(jobs);

        return await root.Parse(args).InvokeAsync(new InvocationConfiguration { Output = error, Error = error });
    }

    private async Task<int> Run(Func<Task> action)
    {
        try
        {
            await action();
            return Success;
        }
        catch (NotSignedInException e)
        {
            await error.WriteLineAsync($"Not signed in: {e.Message} Run 'zoho login'.");
            return NotSignedIn;
        }
        catch (OperationCanceledException)
        {
            await error.WriteLineAsync("Cancelled.");
            return Failure;
        }
        catch (Exception e) when (e is ZohoException or HttpRequestException or IOException or ArgumentException)
        {
            await error.WriteLineAsync(e.Message);
            return Failure;
        }
    }

    private async Task LoginAsync(CancellationToken ct)
    {
        await error.WriteLineAsync("Opening the browser to sign in to Zoho People...");
        var token = await oauth.LoginInBrowserAsync(ct);
        token.UserEmail = await oauth.GetUserEmailAsync(token, ct);
        await tokens.SaveAsync(token);

        var profile = await zoho.GetProfileAsync(ct);
        token.UserId = profile.RecordId;
        await tokens.SaveAsync(token);
        await error.WriteLineAsync($"Signed in as {profile.Email}.");
        Write(new ProfileOutput(profile.Email, profile.EmployeeId), OutputJson.ProfileOutput);
    }

    private async Task MeAsync(CancellationToken ct)
    {
        var profile = await zoho.GetProfileAsync(ct);
        Write(new ProfileOutput(profile.Email, profile.EmployeeId), OutputJson.ProfileOutput);
    }

    private async Task LeavesAsync(DateOnly from, DateOnly to, CancellationToken ct)
    {
        if (to < from) throw new ArgumentException("--to must not be before --from.");
        Write(await new LeaveService(zoho).GetAsync(from, to, ct), OutputJson.ListLeave);
    }

    private async Task JobsAsync(bool refresh, CancellationToken ct)
    {
        var token = await zoho.GetTokenAsync(ct);
        var key = new JobsCacheKey(token.UserId, token.UserEmail, zoho.Settings.Domain);
        var jobs = refresh ? null : await jobsCache.LoadAsync(key, ct);
        if (jobs == null)
        {
            jobs = await new JobService(zoho).GetAsync(ct);
            await jobsCache.SaveAsync(key, jobs, ct);
        }

        Write(jobs, OutputJson.ListJob);
    }

    /// <summary>Serializers for stdout that keep accents and apostrophes readable rather than escaped.</summary>
    private static readonly ZohoJson OutputJson = new(new JsonSerializerOptions(ZohoJson.Default.Options)
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    });

    private void Write<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        output.WriteLine(JsonSerializer.Serialize(value, typeInfo));
    }

    private static DateOnly ParseDate(string value, string option)
    {
        return DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : throw new ArgumentException($"{option} must be a date in yyyy-MM-dd format, not '{value}'.");
    }
}
