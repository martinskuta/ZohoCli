using System.CommandLine;
using ZohoCLI;
using ZohoCLI.Commands;

var commandFactory = new CommandFactory();
var rootCommand = new RootCommand("Zoho People CLI - Manage timesheets and authentication")
{
    ConfigureAuthCommands(),
    ConfigureJobsCommands(),
    ConfigureLeaveCommands()
};

return await rootCommand.InvokeAsync(args);

Command ConfigureAuthCommands()
{
    var authCommand = new Command("auth", "Authentication commands");
    
    var statusCommand = new Command("status", "Check if user is authenticated");
    statusCommand.SetHandler(() => commandFactory.CreateAuthStatusCommand().Execute());
    authCommand.Add(statusCommand);

    var loginCommand = new Command("login", "Log in to Zoho People via OAuth2");
    loginCommand.SetHandler(() => commandFactory.CreateAuthLoginCommand().Execute());
    authCommand.Add(loginCommand);

    var logoutCommand = new Command("logout", "Logout from Zoho People");
    logoutCommand.SetHandler(() => commandFactory.CreateAuthLogoutCommand().Execute());
    authCommand.Add(logoutCommand);

    return authCommand;
}

Command ConfigureJobsCommands()
{
    var jobsCommand = new Command("jobs", "Jobs API related commands. Useful to get client name/id, project name/id, job name/id of jobs assigned to a user to fill out timesheets");

    var getJobsCommand = new Command("get", "Get jobs assigned to you");
    getJobsCommand.SetHandler(() => commandFactory.CreateJobsGetCommand().Execute());
    jobsCommand.Add(getJobsCommand);
    
    return jobsCommand;
}

Command ConfigureLeaveCommands()
{
    var leaveCommand = new Command("leave", "Leave API related commands. Useful to get leave or holiday details of a user");
    var getCommand = new Command("get", "Get all leave details for the authenticated user");

    var getAllCommand = new Command("all", "Get all leave and holiday details for the authenticated user");
    var fromDateOption = new Option<string>("--fromDate", $"From date to get leave details from (inclusive). Default format is {UriFormatter.DefaultDateFormat}");
    fromDateOption.IsRequired = true;
    fromDateOption.AddAlias("-f");
    
    var toDateOption = new Option<string>("--toDate", $"To date to get leave details to (inclusive). Default format is {UriFormatter.DefaultDateFormat}");
    toDateOption.IsRequired = true;
    toDateOption.AddAlias("-t");
    
    var dateFormatOption = new Option<string>("--dateFormat", "Date format to use for dates in the output");
    dateFormatOption.IsRequired = false;
    dateFormatOption.SetDefaultValue(UriFormatter.DefaultDateFormat);
    dateFormatOption.AddAlias("-df");
    
    getAllCommand.AddOption(fromDateOption);
    getAllCommand.AddOption(toDateOption);
    getAllCommand.AddOption(dateFormatOption);
    getAllCommand.SetHandler(ctx =>
    {
        var dateFormat = ctx.ParseResult.GetValueForOption(dateFormatOption)!;
        var fromDate = ParseDateOnly(ctx.ParseResult.GetValueForOption(fromDateOption)!, dateFormat);
        var toDate = ParseDateOnly(ctx.ParseResult.GetValueForOption(toDateOption)!, dateFormat);

        return commandFactory.CreateLeaveGetAllCommand(fromDate, toDate).Execute();
    });
    getCommand.Add(getAllCommand);

    leaveCommand.Add(getCommand);
    
    return leaveCommand;
}

DateOnly ParseDateOnly(string date, string dateFormat)
{
    if(DateOnly.TryParseExact(date, dateFormat, out var parsedDate))
    {
        return parsedDate;
    }
    
    Console.Error.WriteLine($"Parse exception. Date {date} doesn't match the expected date format {dateFormat}.");
    Environment.Exit(1);
    return default;
}