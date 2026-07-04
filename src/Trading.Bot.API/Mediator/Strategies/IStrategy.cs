namespace Trading.Bot.API.Mediator.Strategies;

public interface IStrategy
{
    StrategyType Type { get; }

    IResult Run(RunStrategyRequest request);
}
