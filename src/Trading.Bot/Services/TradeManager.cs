namespace Trading.Bot.Services;

public class TradeManager : BackgroundService
{
    private readonly ILogger<TradeManager> _logger;
    private readonly OandaApiService _apiService;
    private readonly LiveTradeCache _liveTradeCache;
    private readonly TradeConfiguration _tradeConfiguration;
    private readonly EmailService _emailService;
    private readonly TradeSettings[] _tradeSettings;
    private readonly Dictionary<string, TradeSettings> _settingsByInstrument;
    private readonly Dictionary<string, Instrument> _instrumentsByName = [];
    private readonly Dictionary<string, bool> _pairsReady;
    private readonly int _maxDegreeOfParallelism;
    private readonly object _pairsReadyLock = new();
    private readonly string _instrumentNames;
    private int _pairsReadyCount;
    private const int AdditionalCandles = 20;
    private static readonly JsonSerializerOptions EmailJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true
    };

    public TradeManager(ILogger<TradeManager> logger, OandaApiService apiService,
        LiveTradeCache liveTradeCache, TradeConfiguration tradeConfiguration, EmailService emailService)
    {
        _logger = logger;
        _apiService = apiService;
        _liveTradeCache = liveTradeCache;
        _tradeConfiguration = tradeConfiguration;
        _emailService = emailService;
        _tradeSettings = tradeConfiguration.TradeSettings;
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
        if (!_settingsByInstrument.TryGetValue(price.Instrument, out var settings))
        {
            return;
        }

        if (!await ReadyToTrade(price, settings, stoppingToken)) return;

        if (!_tradeConfiguration.PairsTrading)
        {
            await CalculateTrade(settings);
            return;
        }

        if (AllPairsReady(price.Instrument))
        {
            await CalculatePairsTrading(_tradeSettings);
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
            var currentTime = await _apiService.GetLastCandleTime(settings.Instrument, settings.MainGranularity);

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

    private async Task CalculateTrade(TradeSettings settings)
    {
        var granularities = new[] { settings.MainGranularity }.Concat(settings.OtherGranularities);

        var candles = await Task.WhenAll(granularities.Select(g =>
            _apiService.GetCandles(settings.Instrument, g, count: settings.Integers[0] + AdditionalCandles)));

        if (candles.Length == 0 || candles.Any(c => c.Length == 0))
        {
            _logger.LogInformation("Not placing a trade for {Instrument}, candles not found", settings.Instrument);
            return;
        }

        var calcResults = candles.Select(c =>
            c.CalcTrendBreakout(settings.Integers[0], settings.MaxSpread, settings.RiskReward).Last()).ToArray();
        
        var currentIndicator = calcResults[0];

        var openTrades = await _apiService.GetOpenTrades();

        if (openTrades.FirstOrDefault(ot => ot.Instrument == settings.Instrument) is { } openTrade)
        {
            if (await CloseOppositeTrade(currentIndicator, openTrade) ||
                await UpdateWinningTrade(currentIndicator, openTrade))
            {
                openTrades = openTrades.Where(ot => ot.Id != openTrade.Id).ToArray();
            }
        }
        
        var primarySignal = currentIndicator.Signal;
        
        if (primarySignal != Signal.None && calcResults.All(cr => cr.Signal == primarySignal))
        {
            await TryExecuteTrade(settings, currentIndicator, openTrades);
            return;
        }

        _logger.LogInformation("Not placing a trade for {Instrument} based on the indicator", settings.Instrument);
    }

    private async Task CalculatePairsTrading(TradeSettings[] tradeSettings)
    {
        var candles = await Task.WhenAll(tradeSettings.Select(settings =>
            _apiService.GetCandles(settings.Instrument, settings.MainGranularity,
                count: settings.Integers[0] + AdditionalCandles)));

        if (candles.Length == 0 || candles.Any(c => c.Length == 0))
        {
            _logger.LogInformation("Not placing a trade for {Pairs}, candles not found", _instrumentNames);
            return;
        }

        var calcResult = candles[0].CalcMaDistanceZScore(candles[1], tradeSettings[0].Integers[0]).Last();

        if (calcResult.UnitsA == 0 || calcResult.UnitsB == 0)
        {
            calcResult.UnitsA = 5000;
            calcResult.UnitsB = 5000;
        }

        var allOpenTrades = await _apiService.GetOpenTrades();

        var pairInstruments = tradeSettings.Select(s => s.Instrument).ToHashSet(StringComparer.Ordinal);
        
        var pairTrades = allOpenTrades.Where(ot => pairInstruments.Contains(ot.Instrument)).ToArray();

        if (ShouldExitPairsTrade(pairTrades, calcResult, _tradeConfiguration.TradeRisk))
        {
            await Task.WhenAll(pairTrades.Select(trade => _apiService.CloseTrade(trade.Id)));

            pairTrades = [];
        }

        if (calcResult.Signal != Signal.None)
        {
            await TryExecutePairsTrade(tradeSettings, calcResult, pairTrades);
            return;
        }

        _logger.LogInformation("Not placing a trade for {Pairs} based on the indicator", _instrumentNames);
    }

    private async Task TryExecuteTrade(TradeSettings settings, IndicatorResult indicator, TradeResponse[] openTrades)
    {
        if (openTrades.Any(ot => ot.Instrument == settings.Instrument))
        {
            _logger.LogInformation("Cannot place trade for {Instrument}, already open.", settings.Instrument);
            return;
        }

        if (!_instrumentsByName.TryGetValue(settings.Instrument, out var instrument))
        {
            _logger.LogInformation("Cannot place trade for {Instrument}, not found in config.", settings.Instrument);
            return;
        }

        await ExecuteTrade(settings, indicator, instrument);
    }

    private async Task TryExecutePairsTrade(TradeSettings[] tradeSettings, PairsIndicatorResult indicator,
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

        await Task.WhenAll(tradeSettings.Select(settings => ExecuteTrade(settings, indicator,
            _instrumentsByName[settings.Instrument])));
    }

    private async Task ExecuteTrade(TradeSettings settings, IndicatorResult indicator, Instrument instrument)
    {
        var tradeUnits = await GetTradeUnits(settings, indicator);

        if (tradeUnits == 0)
        {
            _logger.LogWarning("Cannot place trade for {Instrument}, unable to calculate trade units",
                settings.Instrument);
            return;
        }

        var trailingStop = settings.TrailingStop ? CalcTrailingStop(indicator, settings.RiskReward) : 0;

        var stopLoss = settings.TrailingStop ? 0 : indicator.StopLoss;

        var order = new Order(instrument, tradeUnits, indicator.Signal, stopLoss, indicator.TakeProfit, trailingStop);

        var ofResponse = _tradeConfiguration.NotifyOnly switch
        {
            true => new OrderFilledResponse(),
            false => await _apiService.PlaceTrade(order)
        };

        if (ofResponse is null)
        {
            _logger.LogWarning("Failed to place order for {Instrument}", settings.Instrument);
            return;
        }

        if (_tradeConfiguration.SendEmail)
        {
            await SendEmailNotification(new
            {
                settings.Instrument,
                Signal = indicator.Signal.ToString()
            });
        }
    }

    private async Task ExecuteTrade(TradeSettings settings, PairsIndicatorResult indicator, Instrument instrument)
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
            return;
        }

        if (_tradeConfiguration.SendEmail)
        {
            await SendEmailNotification(new
            {
                settings.Instrument,
                Signal = signal.ToString()
            });
        }
    }

    private static decimal CalcTrailingStop(IndicatorResult indicator, decimal multiplier)
    {
        return indicator.Gain * multiplier;
    }

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

    private async Task<decimal> GetTradeUnits(TradeSettings settings, IndicatorResult indicator)
    {
        var price = (await _apiService.GetPrices(settings.Instrument)).FirstOrDefault();

        if (price is null) return 0;

        var pipLocation = _instrumentsByName.TryGetValue(settings.Instrument, out var instrument)
            ? instrument.PipLocation
            : 1;

        var numPips = indicator.Loss / pipLocation;

        var perPipLoss = _tradeConfiguration.TradeRisk / numPips;

        return perPipLoss / (price.HomeConversion * pipLocation);
    }

    private static bool ShouldExitPairsTrade(TradeResponse[] openTrades, PairsIndicatorResult indicator,
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

    private async Task<bool> CloseOppositeTrade(IndicatorResult indicator, TradeResponse openTrade)
    {
        if (indicator.Signal is Signal.None) return false;

        var shouldCloseTrade = openTrade.InitialUnits > 0
            ? indicator.Signal is Signal.Sell
            : indicator.Signal is Signal.Buy;

        if (!shouldCloseTrade) return false;

        await _apiService.CloseTrade(openTrade.Id);

        return true;
    }

    private async Task<bool> UpdateWinningTrade(IndicatorResult indicator, TradeResponse openTrade)
    {
        var currentValue = openTrade.InitialUnits > 0
            ? indicator.Candle.Ask_C
            : indicator.Candle.Bid_C;

        if (!ShouldAddTrailingStop(openTrade, currentValue)) return false;

        if (!_instrumentsByName.TryGetValue(openTrade.Instrument, out var instrument))
        {
            _logger.LogInformation("Cannot update trade for {Instrument}, not found in config.", openTrade.Instrument);
            return false;
        }

        var displayPrecision = instrument.DisplayPrecision;

        var trailingStop = Math.Abs(currentValue - openTrade.Price) - indicator.Candle.Spread;

        var update = new OrderUpdate(displayPrecision: displayPrecision, trailingStop: trailingStop);

        await _apiService.UpdateTrade(update, openTrade.Id);

        return true;
    }

    private static bool ShouldAddTrailingStop(TradeResponse trade, decimal currentValue)
    {
        if (trade.TakeProfitOrder is null) return false;

        var priceDistance = Math.Abs(currentValue - trade.Price);
        
        var takeProfitDistance = Math.Abs(currentValue - trade.TakeProfitOrder.Price);
        
        var closest = priceDistance <= takeProfitDistance
            ? trade.Price
            : trade.TakeProfitOrder.Price;

        return trade.TrailingStopLossOrder is null && trade.TakeProfitOrder.Price - closest == 0;
    }
}