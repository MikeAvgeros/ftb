namespace Trading.Bot.Services;

public class LiveTradeCache
{
    public readonly ConcurrentDictionary<string, LivePrice> LivePrices = new();

    public readonly Channel<LivePrice> LivePriceChannel = Channel.CreateUnbounded<LivePrice>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });
}
