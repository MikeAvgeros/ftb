namespace Trading.Bot.API.Diagnostics;

public static class ApplicationDiagnostics
{
    public const string ActivitySourceName = "Trading.Bot.API";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}