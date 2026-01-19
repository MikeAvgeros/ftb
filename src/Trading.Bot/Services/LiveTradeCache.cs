namespace Trading.Bot.Services;

public class LiveTradeCache
{
    public readonly Dictionary<string, LivePrice> LivePrices = [];

    public readonly Channel<LivePrice> LivePriceChannel = Channel.CreateUnbounded<LivePrice>();
}