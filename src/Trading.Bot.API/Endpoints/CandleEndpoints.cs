namespace Trading.Bot.API.Endpoints;

public static class CandleEndpoints
{
    public static void MapCandleEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapGet("api/candles", GetCandles).DisableAntiforgery();
    }

    private static async Task<IResult> GetCandles(ISender sender,
        [AsParameters] CandlesRequest request)
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