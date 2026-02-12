namespace Trading.Bot.API.Middleware;

public class ActivityMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next ??  throw new ArgumentNullException(nameof(next));

    public async Task InvokeAsync(HttpContext context)
    {
        var request = context.Request;

        using var activity = ApplicationDiagnostics.ActivitySource.StartActivity(
            $"{request.Method}:{request.Path}", ActivityKind.Server);
        
        try
        {
            await _next.Invoke(context).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            activity?.AddException(e);

            activity?.SetStatus(ActivityStatusCode.Error);

            throw;
        }
    }
}