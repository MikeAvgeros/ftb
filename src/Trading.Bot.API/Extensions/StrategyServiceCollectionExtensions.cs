namespace Trading.Bot.API.Extensions;

public static class StrategyServiceCollectionExtensions
{
    public static IServiceCollection AddSimulationStrategies(this IServiceCollection services)
    {
        return services
            .AddSingleton<IStrategy, MaCrossStrategy>()
            .AddSingleton<IStrategy, RsiEmaStrategy>()
            .AddSingleton<IStrategy, MacdEmaStrategy>()
            .AddSingleton<IStrategy, MeanReversionStrategy>()
            .AddSingleton<IStrategy, TrendReversionStrategy>()
            .AddSingleton<IStrategy, TrendBreakoutStrategy>()
            .AddSingleton<IStrategy, TrendMomentumStrategy>()
            .AddSingleton<IStrategy, TrendPullbackStrategy>()
            .AddSingleton<IStrategy, MikeStrategy>()
            .AddSingleton<IStrategy, EliasStrategy>()
            .AddSingleton<IStrategy, PairsTradingStrategy>();
    }
}
