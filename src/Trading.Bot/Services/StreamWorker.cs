namespace Trading.Bot.Services;

public class StreamWorker : BackgroundService
{
    private readonly ILogger<StreamWorker> _logger;
    private readonly OandaStreamService _streamService;
    private readonly string _instruments;

    private static readonly TimeSpan MinReconnectDelay = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan StableConnectionThreshold = TimeSpan.FromMinutes(1);

    public StreamWorker(ILogger<StreamWorker> logger, OandaStreamService streamService,
        TradeConfiguration tradeConfiguration)
    {
        _logger = logger;
        _streamService = streamService;
        _instruments = string.Join(',', tradeConfiguration.TradeSettings.Select(s => s.Instrument));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconnectDelay = MinReconnectDelay;

        while (!stoppingToken.IsCancellationRequested)
        {
            var connectedAt = DateTimeOffset.UtcNow;

            await _streamService.StreamLivePrices(_instruments, stoppingToken);

            if (stoppingToken.IsCancellationRequested) break;

            var duration = DateTimeOffset.UtcNow - connectedAt;
            
            if (duration >= StableConnectionThreshold)
                reconnectDelay = MinReconnectDelay;

            _logger.LogWarning(
                "Price stream disconnected after {Duration:g}. Reconnecting in {Delay}s",
                duration, reconnectDelay.TotalSeconds);

            try
            {
                await Task.Delay(reconnectDelay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            
            reconnectDelay = reconnectDelay * 2 < MaxReconnectDelay
                ? reconnectDelay * 2
                : MaxReconnectDelay;
        }
    }
}