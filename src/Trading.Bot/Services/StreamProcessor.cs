namespace Trading.Bot.Services;

public class StreamProcessor : BackgroundService
{
    private readonly ILogger<StreamProcessor> _logger;
    private readonly LiveTradeCache _liveTradeCache;
    private readonly string[] _instruments;
    private readonly Dictionary<string, TimeSpan> _candleSpansByInstrument;
    private readonly ConcurrentDictionary<string, DateTime> _lastCandleTimings = new();
    private readonly int _maxDegreeOfParallelism;
    
    private static readonly TimeSpan BoundaryLeadTime = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan NearBoundaryPollInterval = TimeSpan.FromMilliseconds(200);
    private static readonly TimeSpan MaxSleepDuration = TimeSpan.FromDays(1);

    public StreamProcessor(ILogger<StreamProcessor> logger, LiveTradeCache liveTradeCache,
        TradeConfiguration tradeConfiguration)
    {
        _logger = logger;
        _liveTradeCache = liveTradeCache;
        _maxDegreeOfParallelism = tradeConfiguration.TradeSettings.Length;
        _instruments = tradeConfiguration.TradeSettings.Select(s => s.Instrument).ToArray();
        _candleSpansByInstrument = tradeConfiguration.TradeSettings
            .ToDictionary(s => s.Instrument, s => s.CandleSpan);

        var initialTime = DateTime.UtcNow;

        foreach (var tradeSetting in tradeConfiguration.TradeSettings)
        {
            _lastCandleTimings[tradeSetting.Instrument] = initialTime.RoundDown(tradeSetting.CandleSpan);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = stoppingToken
        };

        while (!stoppingToken.IsCancellationRequested)
        {
            var sleepDuration = ComputeSleepDuration();

            if (sleepDuration > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(sleepDuration, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }

            await Parallel.ForEachAsync(_instruments, options, async (instrument, token) =>
            {
                try
                {
                    if (_liveTradeCache.LivePrices.TryGetValue(instrument, out var value))
                    {
                        await DetectNewCandle(value, token);
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error detecting new candle for {Instrument}", instrument);
                }
            });
        }
    }
    
    private TimeSpan ComputeSleepDuration()
    {
        var now = DateTime.UtcNow;
        
        var minTimeToNext = MaxSleepDuration;

        foreach (var instrument in _instruments)
        {
            var timeToNext = _lastCandleTimings[instrument] + _candleSpansByInstrument[instrument] - now;

            if (timeToNext < minTimeToNext)
                minTimeToNext = timeToNext;
        }

        if (minTimeToNext <= BoundaryLeadTime)
            return NearBoundaryPollInterval;

        return minTimeToNext - BoundaryLeadTime;
    }

    private async Task DetectNewCandle(LivePrice livePrice, CancellationToken stoppingToken)
    {
        var candleSpan = _candleSpansByInstrument[livePrice.Instrument];
        
        var current = livePrice.Time.RoundDown(candleSpan);
        
        var last = _lastCandleTimings[livePrice.Instrument];

        if (current <= last) return;
        
        if (!_lastCandleTimings.TryUpdate(livePrice.Instrument, current, last)) return;
        
        await _liveTradeCache.LivePriceChannel.Writer.WriteAsync(livePrice.Snapshot(current), stoppingToken);
    }
}