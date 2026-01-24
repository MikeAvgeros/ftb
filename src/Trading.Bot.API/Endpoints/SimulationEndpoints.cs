namespace Trading.Bot.API.Endpoints;

public static class SimulationEndpoints
{
    public static void MapSimulationEndpoints(this IEndpointRouteBuilder builder)
    {
        builder.MapPost("api/simulation/ma_cross", SimulateMovingAverageCross).DisableAntiforgery();
        builder.MapPost("api/simulation/rsi_ema", SimulateRsiEma).DisableAntiforgery();
        builder.MapPost("api/simulation/macd_ema", SimulateMacdEma).DisableAntiforgery();
        builder.MapPost("api/simulation/mean_reversion", SimulateBollingerBands).DisableAntiforgery();
        builder.MapPost("api/simulation/trend_reversion", SimulateTrendReversion).DisableAntiforgery();
        builder.MapPost("api/simulation/trend_breakout", SimulateTrendBreakout).DisableAntiforgery();
        builder.MapPost("api/simulation/trend_momentum", SimulateBbEma).DisableAntiforgery();
        builder.MapPost("api/simulation/mike_strategy", SimulateMikeStrategy).DisableAntiforgery();
        builder.MapPost("api/simulation/elias_strategy", SimulateEliasStrategy).DisableAntiforgery();
    }

    private static async Task<IResult> SimulateMovingAverageCross(ISender sender,
        [AsParameters] MovingAverageCrossRequest crossRequest)
    {
        try
        {
            return await sender.Send(crossRequest);
        }
        catch (Exception ex)
        {
            return Results.Problem(ex.Message);
        }
    }

    private static async Task<IResult> SimulateBollingerBands(ISender sender,
        [AsParameters] BollingerBandsRequest request)
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

    private static async Task<IResult> SimulateTrendReversion(ISender sender,
        [AsParameters] TrendReversionRequest request)
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

    private static async Task<IResult> SimulateTrendBreakout(ISender sender,
        [AsParameters] TrendBreakoutRequest request)
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

    private static async Task<IResult> SimulateRsiEma(ISender sender,
        [AsParameters] RsiEmaRequest request)
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

    private static async Task<IResult> SimulateMacdEma(ISender sender,
        [AsParameters] MacdEmaRequest request)
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

    private static async Task<IResult> SimulateBbEma(ISender sender,
        [AsParameters] BollingerBandsEmaRequest request)
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

    private static async Task<IResult> SimulateMikeStrategy(ISender sender,
        [AsParameters] MikeStrategyRequest request)
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

    private static async Task<IResult> SimulateEliasStrategy(ISender sender,
        [AsParameters] EliasStrategyRequest request)
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