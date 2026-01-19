namespace Trading.Bot.Services;

public class StreamProcessor : BackgroundService
{
    private readonly ILogger<StreamProcessor> _logger;
    private readonly LiveTradeCache _liveTradeCache;
    private readonly TradeConfiguration _tradeConfiguration;
    private readonly List<string> _instruments = [];
    private readonly Dictionary<string, DateTime> _lastCandleTimings = [];
    private readonly ParallelOptions _options = new();

    public StreamProcessor(ILogger<StreamProcessor> logger, LiveTradeCache liveTradeCache, TradeConfiguration tradeConfiguration)
    {
        _logger = logger;
        _liveTradeCache = liveTradeCache;
        _tradeConfiguration = tradeConfiguration;
        _options.MaxDegreeOfParallelism = _tradeConfiguration.TradeSettings.Length;

        foreach (var tradeSetting in _tradeConfiguration.TradeSettings)
        {
            _instruments.Add(tradeSetting.Instrument);

            _lastCandleTimings[tradeSetting.Instrument] = DateTime.UtcNow.RoundDown(tradeSetting.CandleSpan);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Parallel.ForEachAsync(_instruments, _options, async (instrument, token) =>
            {
                try
                {
                    if (_liveTradeCache.LivePrices.TryGetValue(instrument, out var value))
                    {
                        await DetectNewCandle(value, token);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred when trying to detect a new candle");
                }
            });

            await Task.Delay(10, stoppingToken);
        }
    }

    private async Task DetectNewCandle(LivePrice livePrice, CancellationToken stoppingToken)
    {
        var candleSpan = _tradeConfiguration.TradeSettings.First(x =>
            x.Instrument == livePrice.Instrument).CandleSpan;

        var current = livePrice.Time.RoundDown(candleSpan);

        if (current <= _lastCandleTimings[livePrice.Instrument]) return;

        _lastCandleTimings[livePrice.Instrument] = current;

        livePrice.Time = current;

        await _liveTradeCache.LivePriceChannel.Writer.WriteAsync(livePrice, stoppingToken);
    }
}