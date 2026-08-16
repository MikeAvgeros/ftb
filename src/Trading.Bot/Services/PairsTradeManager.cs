namespace Trading.Bot.Services;

public class PairsTradeManager : BackgroundService
{
    private readonly ILogger<PairsTradeManager> _logger;
    private readonly OandaApiService _apiService;
    private readonly LiveTradeCache _liveTradeCache;
    private readonly TradeConfiguration _tradeConfiguration;
    private readonly EmailService _emailService;
    private readonly TradeSettings[] _tradeSettings;
    private readonly Dictionary<string, TradeSettings> _settingsByInstrument;
    private readonly Dictionary<string, Instrument> _instrumentsByName = [];
    private readonly Dictionary<string, bool> _pairsReady;
    private readonly int _maxDegreeOfParallelism;
    private readonly Lock _pairsReadyLock = new();
    private readonly string _instrumentNames;
    private int _pairsReadyCount;
    private const int AdditionalCandles = 20;
    private static readonly JsonSerializerOptions EmailJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public PairsTradeManager(ILogger<PairsTradeManager> logger, OandaApiService apiService,
        LiveTradeCache liveTradeCache, TradeConfiguration tradeConfiguration, EmailService emailService)
    {
        _logger = logger;
        _apiService = apiService;
        _liveTradeCache = liveTradeCache;
        _tradeConfiguration = tradeConfiguration;
        _emailService = emailService;
        _tradeSettings = tradeConfiguration.TradeSettings;

        if (_tradeSettings.Length != 2)
        {
            throw new InvalidOperationException(
                $"{nameof(PairsTradeManager)} requires exactly two {nameof(TradeSettings)} entries, but {_tradeSettings.Length} were configured.");
        }

        _settingsByInstrument = _tradeSettings.ToDictionary(s => s.Instrument);
        _pairsReady = _tradeSettings.ToDictionary(s => s.Instrument, _ => false);
        _instrumentNames = string.Join(",", _settingsByInstrument.Keys);
        _maxDegreeOfParallelism = Math.Max(1, _tradeSettings.Length);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Initialise();

        await StartTrading(stoppingToken);
    }

    private async Task Initialise()
    {
        foreach (var instrument in await _apiService.GetInstruments(_instrumentNames))
        {
            _instrumentsByName[instrument.Name] = instrument;
        }
    }

    private async Task StartTrading(CancellationToken stoppingToken)
    {
        var options = new ParallelOptions
        {
            MaxDegreeOfParallelism = _maxDegreeOfParallelism,
            CancellationToken = stoppingToken
        };

        await Parallel.ForEachAsync(_liveTradeCache.LivePriceChannel.Reader.ReadAllAsync(stoppingToken),
            options, async (price, token) =>
            {
                try
                {
                    await DetectNewTrade(price, token);
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested)
                {
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while trying to calculate and execute a trade");
                }
            });
    }

    private async Task DetectNewTrade(LivePrice price, CancellationToken stoppingToken)
    {
        if (!_settingsByInstrument.TryGetValue(price.Instrument, out var settings)) return;

        if (!await ReadyToTrade(price, settings, stoppingToken)) return;

        if (AllPairsReady(price.Instrument))
        {
            await CalculateTrade(_tradeSettings);
        }
    }

    private bool AllPairsReady(string instrument)
    {
        lock (_pairsReadyLock)
        {
            if (!_pairsReady[instrument])
            {
                _pairsReady[instrument] = true;
                _pairsReadyCount++;
            }

            if (_pairsReadyCount < _tradeSettings.Length) return false;

            foreach (var key in _settingsByInstrument.Keys)
            {
                _pairsReady[key] = false;
            }

            _pairsReadyCount = 0;

            return true;
        }
    }

    private async Task<bool> ReadyToTrade(LivePrice price, TradeSettings settings, CancellationToken stoppingToken)
    {
        return await NewCandleAvailable(settings, price, stoppingToken) && GoodTradingTime(DateTime.UtcNow);
    }

    private async Task<bool> NewCandleAvailable(TradeSettings settings, LivePrice price, CancellationToken stoppingToken)
    {
        for (var retryCount = 0; retryCount < 10; retryCount++)
        {
            var currentTime =
                await _apiService.GetLastCandleTime(settings.Instrument, settings.MainGranularity, stoppingToken);

            if (TimeMatches(price.Time, currentTime)) return true;
            
            if (retryCount < 9)
            {
                await Task.Delay(1000, stoppingToken);
            }
        }

        _logger.LogWarning("Cannot get candle that matches the live price. Giving up.");
        
        return false;
    }

    private static bool TimeMatches(DateTime priceTime, DateTime currentTime)
    {
        return currentTime.Ticks / TimeSpan.TicksPerSecond == priceTime.Ticks / TimeSpan.TicksPerSecond;
    }

    private static bool GoodTradingTime(DateTime now)
    {
        return now.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday) && now.Hour is >= 8 and < 17;
    }

    private async Task CalculateTrade(TradeSettings[] tradeSettings)
    {
        var window = tradeSettings[0].Integers[0];

        var candles = await Task.WhenAll(tradeSettings.Select(settings =>
            _apiService.GetCandles(settings.Instrument, settings.MainGranularity, count: window + AdditionalCandles)));

        if (candles.Any(c => c.Length == 0))
        {
            _logger.LogInformation("Not placing a trade for {Pairs}, candles not found", _instrumentNames);
            return;
        }

        var calcResult = candles[0].CalcMaDistanceZScore(candles[1], window).Last();

        var allOpenTrades = await _apiService.GetOpenTrades();

        var pairInstruments = tradeSettings.Select(s => s.Instrument).ToHashSet(StringComparer.Ordinal);

        var pairTrades = allOpenTrades.Where(ot => pairInstruments.Contains(ot.Instrument)).ToArray();

        if (ShouldExitTrade(pairTrades, calcResult, _tradeConfiguration.TradeRisk))
        {
            var closeResults = await Task.WhenAll(pairTrades.Select(trade => _apiService.CloseTrade(trade.Id)));

            if (closeResults.All(success => success))
            {
                pairTrades = [];
            }
            else
            {
                _logger.LogError(
                    "Failed to close one or more existing trades for {Pairs}. Skipping this cycle to avoid stacking a new position on top of the unclosed one.",
                    _instrumentNames);
                return;
            }
        }

        if (calcResult.Signal != Signal.None)
        {
            await TryExecuteTrade(tradeSettings, calcResult, pairTrades);
            return;
        }

        _logger.LogInformation("Not placing a trade for {Pairs} based on the indicator", _instrumentNames);
    }

    private async Task TryExecuteTrade(TradeSettings[] tradeSettings, PairsIndicatorResult indicator,
        TradeResponse[] pairTrades)
    {
        if (pairTrades.Length > 0)
        {
            _logger.LogInformation("Cannot place trade for {Pairs}, already open.", _instrumentNames);
            return;
        }

        if (tradeSettings.Any(settings => !_instrumentsByName.ContainsKey(settings.Instrument)))
        {
            _logger.LogInformation("Cannot place trade for {Pairs}, not found in config.", _instrumentNames);
            return;
        }

        var results = await Task.WhenAll(tradeSettings.Select(settings => ExecuteTrade(settings, indicator,
            _instrumentsByName[settings.Instrument])));

        if (results.All(r => r.Success)) return;

        var filledTradeIds = results.Where(r => r.TradeId is not null).Select(r => r.TradeId).ToArray();

        if (filledTradeIds.Length == 0) return;

        _logger.LogError(
            "Partial fill for pair {Pairs}: one leg failed to open. Closing {Count} filled leg(s) to avoid a naked, unhedged position.",
            _instrumentNames, filledTradeIds.Length);

        var rollbackResults = await Task.WhenAll(filledTradeIds.Select(id => _apiService.CloseTrade(id)));

        if (!rollbackResults.All(success => success))
        {
            _logger.LogError(
                "Failed to flatten one or more filled legs for {Pairs} after a partial fill. Manual intervention required.",
                _instrumentNames);
        }

        if (_tradeConfiguration.SendEmail)
        {
            await SendEmailNotification(new
            {
                Pairs = _instrumentNames,
                Error = "Partial fill detected while opening pair trade; attempted to flatten the filled leg(s)."
            });
        }
    }

    private async Task<TradeExecutionResult> ExecuteTrade(TradeSettings settings, PairsIndicatorResult indicator, Instrument instrument)
    {
        var isPrimary = _tradeConfiguration.TradeSettings[0].Instrument == settings.Instrument;

        var tradeUnits = isPrimary ? indicator.UnitsA : indicator.UnitsB;

        var signal = isPrimary ? indicator.Signal : GetOppositeSignal(indicator.Signal);

        var order = new Order(instrument, tradeUnits, signal);

        var ofResponse = _tradeConfiguration.NotifyOnly switch
        {
            true => new OrderFilledResponse(),
            false => await _apiService.PlaceTrade(order)
        };

        if (ofResponse is null)
        {
            _logger.LogWarning("Failed to place order for {Instrument}", settings.Instrument);
            return new TradeExecutionResult(false, null);
        }

        if (_tradeConfiguration.SendEmail)
        {
            await SendEmailNotification(new
            {
                settings.Instrument,
                Signal = signal.ToString()
            });
        }

        return new TradeExecutionResult(true, ofResponse.TradeOpened?.TradeID);
    }

    private readonly record struct TradeExecutionResult(bool Success, string TradeId);

    private static Signal GetOppositeSignal(Signal signal)
    {
        return signal is Signal.Buy ? Signal.Sell : Signal.Buy;
    }

    private async Task SendEmailNotification(object emailBody)
    {
        await _emailService.SendMailAsync(new EmailData
        {
            EmailToAddress = "mike.avgeros@gmail.com",
            EmailToName = "Mike",
            EmailSubject = "New Trade",
            EmailBody = JsonSerializer.Serialize(emailBody, EmailJsonOptions)
        });
    }

    private static bool ShouldExitTrade(TradeResponse[] openTrades, PairsIndicatorResult indicator,
        int tradeRisk)
    {
        if (openTrades.Length == 0) return false;

        var totalPl = openTrades.Sum(ot => ot.UnrealizedPL);

        return CanTakeProfit(indicator, totalPl) ||
               HasOverExposureWithProfit(DateTime.UtcNow, openTrades) ||
               indicator.StopLoss || totalPl < -tradeRisk;
    }

    private static bool CanTakeProfit(PairsIndicatorResult indicator, decimal totalPl)
    {
        return indicator.TakeProfit && totalPl > 0;
    }

    private static bool HasOverExposureWithProfit(DateTime currentTime, TradeResponse[] openTrades)
        => openTrades.Any(ot => currentTime.Subtract(ot.OpenTime) >= TimeSpan.FromHours(1)) &&
           openTrades.Sum(ot => ot.UnrealizedPL) > 0;
}