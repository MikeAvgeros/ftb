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
    private readonly ConcurrentDictionary<string, bool> _pairsReady = [];

    public TradeManager(ILogger<TradeManager> logger, OandaApiService apiService,
        LiveTradeCache liveTradeCache, TradeConfiguration tradeConfiguration, EmailService emailService)
    {
        _logger = logger;
        _apiService = apiService;
        _liveTradeCache = liveTradeCache;
        _tradeConfiguration = tradeConfiguration;
        _emailService = emailService;
        _options.MaxDegreeOfParallelism = _tradeConfiguration.TradeSettings.Length;
        _pairsReady = new ConcurrentDictionary<string, bool>(tradeConfiguration.TradeSettings.ToDictionary(s => s.Instrument, s => false));
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

        if (_pairsReady.All(p => p.Value == true))
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
            _apiService.GetCandles(settings.Instrument, g, count: settings.Integers[0] + 20)));

        if (candles.Length == 0 || candles.Any(c => c.Length == 0))
        {
            _logger.LogInformation("Not placing a trade for {Instrument}, candles not found", settings.Instrument);
        }

        var calcResults = candles.Select(c =>
            c.CalcTrendBreakout(settings.Integers[0], settings.MaxSpread, settings.RiskReward).Last()).ToList();

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
            await TryPlaceTrade(settings, calcResults.First(), openTrades);
        }

        _logger.LogInformation("Not placing a trade for {Instrument} based on the indicator", settings.Instrument);
    }

    private async Task CalculatePairsTrading(TradeSettings[] tradeSettings)
    {
        var candles = await Task.WhenAll(tradeSettings.Select(settings =>
            _apiService.GetCandles(settings.Instrument, settings.MainGranularity, count: settings.Integers[0] + 20)));

        if (candles.Length == 0 || candles.Any(c => c.Length == 0))
        {
            _logger.LogInformation("Not placing a trade for {Pairs}, candles not found",
                string.Join(",", tradeSettings.Select(s => s.Instrument)));
        }

        var calcResult = candles[0].CalcPairsTrading(candles[1]);

        var openTrades = await _apiService.GetOpenTrades();

        if (ShouldExitPairsTrade(openTrades, calcResult))
        {
            foreach (var trade in openTrades)
            {
                await _apiService.CloseTrade(trade.Id);
            }

            openTrades = [];
        }

        await TryPlacePairsTrade(tradeSettings, calcResult, openTrades);
    }

    private async Task TryPlaceTrade(TradeSettings settings, IndicatorBase indicator, TradeResponse[] openTrades)
    {
        if (openTrades.Any(ot => ot.Instrument == settings.Instrument))
        {
            _logger.LogInformation("Cannot place trade for {Instrument}, already open.", settings.Instrument);
            return;
        }

        var instrument = _instruments.FirstOrDefault(i => i.Name == settings.Instrument);

        if (instrument is null) return;

        var tradeUnits = await GetTradeUnits(settings, indicator);

        var trailingStop = settings.TrailingStop ? CalcTrailingStop(indicator, settings.RiskReward) : 0;

        var stopLoss = settings.TrailingStop ? 0 : indicator.StopLoss;

        var order = new Order(instrument, tradeUnits, indicator.Signal, indicator.StopLoss, indicator.TakeProfit, trailingStop);

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
                Signal = indicator.Signal.ToString(),
                Units = ofResponse.TradeOpened?.Units ?? Math.Round(tradeUnits, instrument.TradeUnitsPrecision),
                Price = ofResponse.TradeOpened?.Price ?? indicator.Candle.Mid_C,
                TakeProfit = order.TakeProfitOnFill?.Price ?? 0,
                StopLoss = order.StopLossOnFill?.Price ?? 0,
                TrailingStop = order.TrailingStopLossOnFill?.Distance ?? 0
            });
        }
    }

    private async Task TryPlacePairsTrade(TradeSettings[] tradeSettings, PairsIndicatorResult indicator, TradeResponse[] openTrades)
    {
        if (indicator.Signal is Signal.None || openTrades.Length > 0) return;

        foreach (var settings in tradeSettings)
        {
            var instrument = _instruments.FirstOrDefault(i => i.Name == settings.Instrument);

            if (instrument is null) return;

            var tradeUnits = tradeSettings[0] == settings ? indicator.UnitsA : indicator.UnitsB;

            var order = new Order(instrument, tradeUnits, indicator.Signal, 0, 0, 0);

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
        }

        if (_tradeConfiguration.SendEmail)
        {
            await SendEmailNotification(new
            {
                Pair = string.Join(",", tradeSettings.Select(s => s.Instrument)),
                Signal = indicator.Signal.ToString()
            });
        }
    }

    private static decimal CalcTrailingStop(IndicatorBase indicator, decimal multiplier)
    {
        return indicator.Gain * multiplier;
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

    private async Task<decimal> GetTradeUnits(TradeSettings settings, IndicatorBase indicator)
    {
        var price = (await _apiService.GetPrices(settings.Instrument)).FirstOrDefault();

        if (price is null) return 0;

        var pipLocation = _instruments.FirstOrDefault(i => i.Name == settings.Instrument)?.PipLocation ?? 1;

        var numPips = indicator.Loss / pipLocation;

        var perPipLoss = _tradeConfiguration.TradeRisk / numPips;

        return perPipLoss / (price.HomeConversion * pipLocation);
    }

    private static bool ShouldExitPairsTrade(TradeResponse[] openTrades, PairsIndicatorResult indicator)
    {
        return openTrades.Length > 0 && (indicator.Exit || openTrades.Any(ot =>
                DateTime.UtcNow.Subtract(ot.OpenTime) > TimeSpan.FromHours(2)));
    }

    private async Task<bool> CloseOppositeTrade(IndicatorBase indicator, TradeResponse openTrade)
    {
        if (indicator.Signal is Signal.None || openTrade is null) return false;

        var shouldCloseTrade = openTrade.InitialUnits > 0
            ? indicator.Signal is Signal.Sell
            : indicator.Signal is Signal.Buy;

        if (shouldCloseTrade)
        {
            await _apiService.CloseTrade(openTrade.Id);

            return true;
        }

        return false;
    }

    private async Task<bool> UpdateWinningTrade(IndicatorBase indicator, TradeResponse openTrade)
    {
        if (openTrade is null) return false;

        var currentValue = openTrade.InitialUnits > 0
            ? indicator.Candle.Ask_C
            : indicator.Candle.Bid_C;

        if (ShouldAddTrailingStop(openTrade, currentValue))
        {
            var displayPrecision = _instruments.First(i => i.Name == openTrade.Instrument).DisplayPrecision;

            var trailingStop = Math.Abs(currentValue - openTrade.Price) - indicator.Candle.Spread;

            var update = new OrderUpdate(displayPrecision: displayPrecision, trailingStop: trailingStop);

            await _apiService.UpdateTrade(update, openTrade.Id);

            return true;
        }

        return false;
    }

    private static bool ShouldAddTrailingStop(TradeResponse trade, decimal currentValue)
    {
        var priceList = new List<decimal> { trade.Price, trade.TakeProfitOrder.Price };

        var closest = priceList.OrderBy(value => Math.Abs(currentValue - value)).First();

        return trade.TrailingStopLossOrder is null && trade.TakeProfitOrder.Price - closest == 0;
    }
}