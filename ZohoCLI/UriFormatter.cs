namespace ZohoCLI;

public static class UriFormatter
{
    public const string DefaultDateFormat = "dd-MMM-yyyy";
    
    public static string FormattedDefaultDateFormat => FormatString(DefaultDateFormat);
    
    public static string FormatDate(DateTime date) => Uri.EscapeDataString(date.ToString(DefaultDateFormat));
    
    public static string FormatDate(DateOnly date) => Uri.EscapeDataString(date.ToString(DefaultDateFormat));
    
    public static string FormatString(string str) => Uri.EscapeDataString(str);
}