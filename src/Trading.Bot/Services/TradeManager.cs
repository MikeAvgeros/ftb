namespace Trading.Bot.Services;

public class TradeManager : BackgroundService
{
    private readonly ILogger<TradeManager> _logger;
    private readonly OandaApiService _apiService;
    private readonly LiveTradeCache _liveTradeCache;
    private readonly TradeConfiguration _tradeConfiguration;
    private readonly EmailService _emailService;
    private readonly List<Instrument> _instruments = [];
    private readonly ParallelOptions _options = new();
    private readonly ConcurrentDictionary<string, bool> _pairsReady;
    private const int AdditionalCandles = 20;

    public TradeManager(ILogger<TradeManager> logger, OandaApiService apiService,
        LiveTradeCache liveTradeCache, TradeConfiguration tradeConfiguration, EmailService emailService)
    {
        _logger = logger;
        _apiService = apiService;
        _liveTradeCache = liveTradeCache;
        _tradeConfiguration = tradeConfiguration;
        _emailService = emailService;
        _options.MaxDegreeOfParallelism = _tradeConfiguration.TradeSettings.Length;
        _pairsReady = new ConcurrentDictionary<string, bool>(
            tradeConfiguration.TradeSettings.ToDictionary(s => s.Instrument, _ => false));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Initialise();

        await StartTrading(stoppingToken);
    }

    private async Task Initialise()
    {
        _instruments.AddRange(await _apiService.GetInstruments(string.Join(",",
            _tradeConfiguration.TradeSettings.Select(s => s.Instrument))));
    }

    private async Task StartTrading(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Parallel.ForEachAsync(_liveTradeCache.LivePriceChannel.Reader.ReadAllAsync(stoppingToken),
                _options, async (price, token) =>
                {
                    try
                    {
                        await DetectNewTrade(price, token);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "An error occurred while trying to calculate and execute a trade");
                    }
                });

            await Task.Delay(10, stoppingToken);
        }
    }

    private async Task DetectNewTrade(LivePrice price, CancellationToken stoppingToken)
    {
        var settings = _tradeConfiguration.TradeSettings.First(x => x.Instrument == price.Instrument);

        if (!await ReadyToTrade(price, settings, stoppingToken)) return;

        if (!_tradeConfiguration.PairsTrading)
        {
            await CalculateTrade(settings);
            return;
        }

        _pairsReady[price.Instrument] = true;

        if (_pairsReady.All(p => p.Value))
        {
            foreach (var tradeSettings in _tradeConfiguration.TradeSettings)
            {
                _pairsReady[tradeSettings.Instrument] = false;
            }

            await CalculatePairsTrading(_tradeConfiguration.TradeSettings);
        }
    }

    private async Task<bool> ReadyToTrade(LivePrice price, TradeSettings settings, CancellationToken stoppingToken)
    {
        return await NewCandleAvailable(settings, price, stoppingToken) && GoodTradingTime();
    }

    private async Task<bool> NewCandleAvailable(TradeSettings settings, LivePrice price, CancellationToken stoppingToken)
    {
        var retryCount = 0;

    Start:

        if (retryCount >= 10)
        {
            _logger.LogWarning("Cannot get candle that matches the live price. Giving up.");
            return false;
        }

        var currentTime = await _apiService.GetLastCandleTime(settings.Instrument, settings.MainGranularity);

        if (TimeMatches(price.Time, currentTime)) return true;

        await Task.Delay(1000, stoppingToken);

        retryCount++;

        goto Start;
    }

    private static bool TimeMatches(DateTime priceTime, DateTime currentTime)
    {
        return new DateTime(currentTime.Year, currentTime.Month, currentTime.Day, currentTime.Hour, currentTime.Minute,
                   currentTime.Second) ==
               new DateTime(priceTime.Year, priceTime.Month, priceTime.Day, priceTime.Hour, priceTime.Minute,
                   priceTime.Second);
    }

    private static bool GoodTradingTime()
    {
        var date = DateTime.UtcNow;

        return date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    private async Task CalculateTrade(TradeSettings settings)
    {
        var granularities = new[] { settings.MainGranularity }.Concat(settings.OtherGranularities);

        var candles = await Task.WhenAll(granularities.Select(g =>
            _apiService.GetCandles(settings.Instrument, g, count: settings.Integers[0] + AdditionalCandles)));

        if (candles.Length == 0 || candles.Any(c => c.Length == 0))
        {
            _logger.LogInformation("Not placing a trade for {Instrument}, candles not found", settings.Instrument);
        }

        var calcResults = candles.Select(c =>
            c.CalcTrendBreakout(settings.Integers[0], settings.MaxSpread, settings.RiskReward).Last()).ToArray();

        var openTrades = await _apiService.GetOpenTrades();

        if (openTrades.FirstOrDefault(ot => ot.Instrument == settings.Instrument) is { } openTrade)
        {
            if (await CloseOppositeTrade(calcResults.First(), openTrade) ||
                await UpdateWinningTrade(calcResults.First(), openTrade))
            {
                openTrades = [.. openTrades.Where(ot => ot.Id != openTrade.Id)];
            }
        }

        if (calcResults.All(cr => cr.Signal != Signal.None))
        {
            await TryExecuteTrade(settings, calcResults.First(), openTrades);
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
            _logger.LogInformation("Not placing a trade for {Pairs}, candles not found",
                string.Join(",", tradeSettings.Select(s => s.Instrument)));
        }

        var calcResult = candles[0].CalcMaDistance(candles[1], tradeSettings[0].Integers[0]).Last();

        if (calcResult.UnitsA == 0 || calcResult.UnitsB == 0)
        {
            calcResult.UnitsA = 4000 + 10000000 * (decimal)candles[0].CalcAtr().Last().Atr;
            calcResult.UnitsB = 4000 + 10000000 * (decimal)candles[1].CalcAtr().Last().Atr;
        }

        var openTrades = await _apiService.GetOpenTrades();

        if (ShouldExitPairsTrade(openTrades, calcResult))
        {
            foreach (var trade in openTrades)
            {
                await _apiService.CloseTrade(trade.Id);
            }

            openTrades = [];
        }

        if (calcResult.Signal != Signal.None)
        {
            await TryExecutePairsTrade(tradeSettings, calcResult, openTrades);
            return;
        }

        _logger.LogInformation("Not placing a trade for {Pairs} based on the indicator",
            string.Join(",", tradeSettings.Select(s => s.Instrument)));
    }

    private async Task TryExecuteTrade(TradeSettings settings, IndicatorResult indicator, TradeResponse[] openTrades)
    {
        if (openTrades.Any(ot => ot.Instrument == settings.Instrument))
        {
            _logger.LogInformation("Cannot place trade for {Instrument}, already open.", settings.Instrument);
            return;
        }

        var instrument = _instruments.FirstOrDefault(i => i.Name == settings.Instrument);

        if (instrument is null)
        {
            _logger.LogInformation("Cannot place trade for {Instrument}, not found in config.", settings.Instrument);
            return;
        }

        await ExecuteTrade(settings, indicator, instrument);
    }

    private async Task TryExecutePairsTrade(TradeSettings[] tradeSettings, PairsIndicatorResult indicator,
        TradeResponse[] openTrades)
    {
        if (openTrades.Length > 0)
        {
            _logger.LogInformation("Cannot place trade for {Pairs}, already open.",
                string.Join(",", tradeSettings.Select(s => s.Instrument)));
            return;
        }

        var instrumentsFound = _instruments.All(i =>
            tradeSettings.Any(t => t.Instrument == i.Name));

        if (!instrumentsFound)
        {
            _logger.LogInformation("Cannot place trade for {Pairs}, not found in config.",
                string.Join(",", tradeSettings.Select(s => s.Instrument)));
            return;
        }

        await Task.WhenAll(tradeSettings.Select(settings => ExecuteTrade(settings, indicator,
            _instruments.First(i => i.Name == settings.Instrument))));
    }

    private async Task ExecuteTrade(TradeSettings settings, IndicatorResult indicator, Instrument instrument)
    {
        var tradeUnits = await GetTradeUnits(settings, indicator);

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

        var tradeUnits = isPrimary
            ? indicator.UnitsA
            : indicator.UnitsB;

        var signal = isPrimary
            ? indicator.Signal
            : GetOppositeSignal(indicator.Signal);

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
                Signal = indicator.Signal.ToString()
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
            EmailBody = JsonSerializer.Serialize(emailBody,
                new JsonSerializerOptions
                {
                    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                    WriteIndented = true
                })
        });
    }

    private async Task<decimal> GetTradeUnits(TradeSettings settings, IndicatorResult indicator)
    {
        var price = (await _apiService.GetPrices(settings.Instrument)).FirstOrDefault();

        if (price is null) return 0;

        var pipLocation = _instruments.FirstOrDefault(i =>
            i.Name == settings.Instrument)?.PipLocation ?? 1;

        var numPips = indicator.Loss / pipLocation;

        var perPipLoss = _tradeConfiguration.TradeRisk / numPips;

        return perPipLoss / (price.HomeConversion * pipLocation);
    }

    private async Task<decimal> GetTradeUnits(TradeSettings settings, Candle[] candles, decimal multiplier)
    {
        var price = (await _apiService.GetPrices(settings.Instrument)).FirstOrDefault();

        if (price is null) return 0;

        var atrResult = candles.CalcAtr();

        var stopDistance = (decimal)atrResult.Last().Atr * multiplier;

        var pipLocation = _instruments.FirstOrDefault(i =>
            i.Name == settings.Instrument)?.PipLocation ?? 1;

        var numPips = stopDistance / pipLocation;

        var perPipLoss = _tradeConfiguration.TradeRisk / numPips;

        return perPipLoss / (price.HomeConversion * pipLocation);
    }

    private static bool ShouldExitPairsTrade(TradeResponse[] openTrades, PairsIndicatorResult indicator)
    {
        if (openTrades.Length == 0) return false;

        return HasPositiveUnrealisedPl(openTrades) || indicator.StopLoss;
    }

    private static bool HasPositiveUnrealisedPl(TradeResponse[] openTrades)
    {
        var totalPl = openTrades.Sum(ot => ot.UnrealizedPL);

        return totalPl > 0;
    }

    private async Task<bool> CloseOppositeTrade(IndicatorResult indicator, TradeResponse openTrade)
    {
        if (indicator.Signal is Signal.None || openTrade is null) return false;

        var shouldCloseTrade = openTrade.InitialUnits > 0
            ? indicator.Signal is Signal.Sell
            : indicator.Signal is Signal.Buy;

        if (!shouldCloseTrade) return false;

        await _apiService.CloseTrade(openTrade.Id);

        return true;
    }

    private async Task<bool> UpdateWinningTrade(IndicatorResult indicator, TradeResponse openTrade)
    {
        if (openTrade is null) return false;

        var currentValue = openTrade.InitialUnits > 0
            ? indicator.Candle.Ask_C
            : indicator.Candle.Bid_C;

        if (!ShouldAddTrailingStop(openTrade, currentValue)) return false;

        var displayPrecision = _instruments.First(i => i.Name == openTrade.Instrument).DisplayPrecision;

        var trailingStop = Math.Abs(currentValue - openTrade.Price) - indicator.Candle.Spread;

        var update = new OrderUpdate(displayPrecision: displayPrecision, trailingStop: trailingStop);

        await _apiService.UpdateTrade(update, openTrade.Id);

        return true;
    }

    private static bool ShouldAddTrailingStop(TradeResponse trade, decimal currentValue)
    {
        var priceList = new List<decimal> { trade.Price, trade.TakeProfitOrder.Price };

        var closest = priceList.OrderBy(value => Math.Abs(currentValue - value)).First();

        return trade.TrailingStopLossOrder is null && trade.TakeProfitOrder.Price - closest == 0;
    }
}