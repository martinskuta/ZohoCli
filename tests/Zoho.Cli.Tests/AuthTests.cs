using System.Net;
using System.Text.Json.Nodes;
using Zoho.Cli.Zoho;

namespace Zoho.Cli.Tests;

public class AuthTests
{
    private const string Profile = """[{ "recordId": "42", "Employee ID": "INN-1", "Email address": "jane@example.com" }]""";

    [Fact]
    public async Task Without_a_token_commands_exit_2_and_say_to_sign_in()
    {
        using var harness = new Harness();

        var exit = await harness.RunAsync("me");

        Assert.Equal(2, exit);
        Assert.Contains("zoho login", harness.Error.ToString());
        Assert.Empty(harness.Output.ToString());
        Assert.Empty(harness.Http.Requests);
    }

    [Fact]
    public async Task Me_prints_the_email_and_employee_id()
    {
        using var harness = new Harness(Harness.ValidToken());
        harness.Http.On("P_EmployeeView", Profile);

        var exit = await harness.RunAsync("me");

        Assert.Equal(0, exit);
        Assert.Equal("""{"email":"jane@example.com","employeeId":"INN-1"}""", harness.Output.ToString().Trim());
        Assert.Contains(harness.Http.Requests, url => url.Contains("searchValue=jane%40example.com"));
    }

    [Fact]
    public async Task An_expired_token_is_refreshed_and_saved_with_the_person_kept()
    {
        var token = Harness.ValidToken();
        token.ExpiresAt = Harness.Now.AddMinutes(-1);
        using var harness = new Harness(token);
        harness.Http
            .On("oauth/v2/token", request =>
            {
                var form = request.Content!.ReadAsStringAsync().Result;
                Assert.Contains("grant_type=refresh_token", form);
                Assert.Contains("refresh_token=refresh", form);
                return (HttpStatusCode.OK, """{ "access_token": "new-access", "expires_in": 3600, "token_type": "Bearer" }""");
            })
            .On("P_EmployeeView", Profile);

        var exit = await harness.RunAsync("me");

        Assert.Equal(0, exit);
        var saved = harness.Tokens.Token!;
        Assert.Equal("new-access", saved.AccessToken);
        Assert.Equal("refresh", saved.RefreshToken);
        Assert.Equal("jane@example.com", saved.UserEmail);
        Assert.Equal(42, saved.UserId);
        Assert.Equal(Harness.Now.AddSeconds(3600 - 15), saved.ExpiresAt);
    }

    [Fact]
    public async Task A_refused_refresh_exits_2()
    {
        var token = Harness.ValidToken();
        token.ExpiresAt = Harness.Now.AddMinutes(-1);
        using var harness = new Harness(token);
        harness.Http.On("oauth/v2/token", """{ "error": "invalid_code" }""");

        var exit = await harness.RunAsync("jobs");

        Assert.Equal(2, exit);
        Assert.Contains("invalid_code", harness.Error.ToString());
        Assert.Equal(0, harness.Tokens.Saves);
    }

    [Fact]
    public async Task A_token_Zoho_refuses_exits_2()
    {
        using var harness = new Harness(Harness.ValidToken());
        harness.Http.On("P_EmployeeView", """{ "code": "INVALID_OAUTHTOKEN" }""", HttpStatusCode.Unauthorized);

        Assert.Equal(2, await harness.RunAsync("me"));
    }

    [Fact]
    public async Task Other_Zoho_errors_exit_1()
    {
        using var harness = new Harness(Harness.ValidToken());
        harness.Http.On("P_EmployeeView", "oops", HttpStatusCode.InternalServerError);

        Assert.Equal(1, await harness.RunAsync("me"));
        Assert.Contains("500", harness.Error.ToString());
    }

    [Fact]
    public void A_record_without_employee_id_names_its_fields()
    {
        var error = Assert.Throws<ZohoException>(() =>
            ZohoClient.ParseProfile("jane@example.com", JsonNode.Parse("""[{ "recordId": "42", "First Name": "Jane" }]""")!));

        Assert.Contains("First Name", error.Message);
    }
}
