using System.Net;
using Zoho.Cli.Auth;
using Zoho.Cli.Zoho;

namespace Zoho.Cli.Tests;

/// <summary>Answers each request with the first route whose key the URL contains.</summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    private readonly List<(string UrlPart, Func<HttpRequestMessage, (HttpStatusCode, string)> Answer)> _routes = [];
    public List<string> Requests { get; } = [];

    public FakeHttp On(string urlPart, string body, HttpStatusCode status = HttpStatusCode.OK)
    {
        _routes.Add((urlPart, _ => (status, body)));
        return this;
    }

    public FakeHttp On(string urlPart, Func<HttpRequestMessage, (HttpStatusCode, string)> answer)
    {
        _routes.Add((urlPart, answer));
        return this;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var url = request.RequestUri!.ToString();
        Requests.Add(url);
        var route = _routes.FirstOrDefault(r => url.Contains(r.UrlPart, StringComparison.Ordinal));
        if (route.Answer == null) throw new InvalidOperationException($"No fake route for {url}");
        var (status, body) = route.Answer(request);
        return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent(body) });
    }
}

internal sealed class MemoryTokenStore(OAuthToken? token = null) : ITokenStore
{
    public OAuthToken? Token { get; private set; } = token;
    public int Saves { get; private set; }

    public Task<OAuthToken?> LoadAsync() => Task.FromResult(Token);

    public Task SaveAsync(OAuthToken token)
    {
        Token = token;
        Saves++;
        return Task.CompletedTask;
    }
}

internal sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
}

/// <summary>A CliApp over fakes, with its stdout and stderr captured.</summary>
internal sealed class Harness : IDisposable
{
    public static readonly DateTimeOffset Now = new(2026, 11, 27, 10, 0, 0, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "zoho-cli-tests-" + Guid.NewGuid().ToString("N"));

    public Harness(OAuthToken? token = null)
    {
        Tokens = new MemoryTokenStore(token);
        Settings = new ZohoSettings("zoho.eu", _directory);
    }

    public FakeHttp Http { get; } = new();
    public MemoryTokenStore Tokens { get; }
    public FixedTime Time { get; } = new(Now);
    public ZohoSettings Settings { get; }
    public StringWriter Output { get; } = new();
    public StringWriter Error { get; } = new();
    public string CachePath => Path.Combine(_directory, "jobs.json");

    public static OAuthToken ValidToken() => new()
    {
        UserEmail = "jane@example.com",
        UserId = 42,
        AccessToken = "access",
        RefreshToken = "refresh",
        ExpiresAt = Now.AddHours(1)
    };

    public ZohoClient Client()
    {
        var http = new HttpClient(Http);
        return new ZohoClient(http, Tokens, new OAuthClient(http, Settings, Time), Settings, Time);
    }

    public Task<int> RunAsync(params string[] args)
    {
        var http = new HttpClient(Http);
        var oauth = new OAuthClient(http, Settings, Time);
        var zoho = new ZohoClient(http, Tokens, oauth, Settings, Time);
        var app = new CliApp(zoho, oauth, Tokens, new JobsCache(CachePath, Time), Output, Error);
        return app.RunAsync(args);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
