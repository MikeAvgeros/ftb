#nullable enable
namespace Trading.Bot.API.Mediator;

public sealed class RunStrategyHandler(IEnumerable<IStrategy> strategies)
    : IRequestHandler<RunStrategyRequest, IResult>
{
    public Task<IResult> Handle(RunStrategyRequest request, CancellationToken cancellationToken)
    {
        var strategy = strategies.FirstOrDefault(s => s.Type == request.StrategyType);

        if (strategy is null)
        {
            return Task.FromResult(Results.Problem($"Unknown strategy '{request.StrategyType}'.",
                statusCode: StatusCodes.Status400BadRequest));
        }

        return Task.FromResult(strategy.Run(request));
    }
}

public sealed record RunStrategyRequest : IHttpRequest
{
    public IFormFileCollection Files { get; set; } = new FormFileCollection();
    public StrategyType StrategyType { get; set; }

    [FromQuery]
    public int[]? Ints { get; set; }

    [FromQuery]
    public double[]? Doubles { get; set; }

    public decimal? MaxSpread { get; set; }
    public decimal? MinGain { get; set; }
    public decimal? RiskReward { get; set; }
    
    public int? TradeRisk { get; set; }

    public bool? UpdateTrade { get; set; }

    public int GetInt(int index, int defaultValue) =>
        Ints is not null && Ints.Length > index ? Ints[index] : defaultValue;

    public double GetDouble(int index, double defaultValue) =>
        Doubles is not null && Doubles.Length > index ? Doubles[index] : defaultValue;
}
