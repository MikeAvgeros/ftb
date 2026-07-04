namespace Trading.Bot.API.Endpoints;

public static class SimulationEndpoints
{
    public static void MapSimulationEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/simulation/run", RunStrategy).DisableAntiforgery();
    }

    private static async Task<IResult> RunStrategy(ISender sender,
        [AsParameters] RunStrategyRequest request)
    {
        try
        {
            return await sender.Send(request);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }
}