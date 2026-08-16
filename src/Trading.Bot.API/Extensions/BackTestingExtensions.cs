namespace Trading.Bot.API.Extensions;

public static class BackTestingExtensions
{
    public static IEnumerable<string> GetAllCombinations(this IEnumerable<string> sequence)
    {
        var list = sequence.ToList();

        if (list.Count == 0)
        {
            yield return string.Empty;
        }
        else
        {
            for (var i = 0; i < list.Count; i++)
            {
                var index = 0;

                while (index < list.Count)
                {
                    if (i == index) index++;

                    if (index == list.Count) break;

                    yield return $"{list[i]}_{list[index]}";

                    index++;
                }
            }
        }
    }

    public static IEnumerable<Tuple<int, int>> GetAllWindowCombinations(this IEnumerable<int> sequence)
    {
        var list = sequence.ToList();

        if (list.Count == 0)
        {
            yield return Tuple.Create(0, 0);
        }
        else
        {
            for (var i = 0; i < list.Count; i++)
            {
                var index = 0;

                while (index < list.Count)
                {
                    if (i == index) index++;

                    if (index == list.Count) break;

                    if (list[i] < list[index])
                    {
                        yield return Tuple.Create(list[i], list[index]);
                    }

                    index++;
                }
            }
        }
    }

    public static byte[] GetCsvBytes<T>(this IEnumerable<T> sequence)
    {
        using var memoryStream = new MemoryStream();

        using (var writer = new StreamWriter(memoryStream))
        {
            using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
            {
                var options = new TypeConverterOptions
                {
                    Formats =
                    [
                        "o"
                    ]
                };

                csv.Context.TypeConverterOptionsCache.AddOptions<DateTime>(options);

                csv.WriteRecords(sequence);
            }
        }

        return memoryStream.ToArray();
    }

    public static IEnumerable<FileData<IEnumerable<object>>> GetFileData(this IndicatorResult[] indicator,
        string fileName, int tradeRisk, decimal riskReward, bool updateTrade = false)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var tradingSim = SimulateTrade(indicator, tradeRisk, riskReward, updateTrade);

        fileData.Add(new FileData<IEnumerable<object>>(
            $"{fileName}.csv", indicator.Where(ma => ma.Signal != Signal.None)));

        fileData.Add(new FileData<IEnumerable<object>>(
            $"{fileName}_Simulation.csv", tradingSim.Result));

        fileData.Add(new FileData<IEnumerable<object>>(
            $"{fileName}_Summary.csv", [tradingSim.Summary]));

        return fileData;
    }

    public static IEnumerable<FileData<IEnumerable<object>>> GetFileData(this PairsIndicatorResult[] indicator,
        string fileName, int tradeRisk)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var tradingSim = SimulatePairsTrade(indicator, tradeRisk);

        fileData.Add(new FileData<IEnumerable<object>>(
            $"{fileName}.csv", indicator.Where(ma => ma.Signal != Signal.None)));

        fileData.Add(new FileData<IEnumerable<object>>(
            $"{fileName}_Simulation.csv", tradingSim.Result));

        fileData.Add(new FileData<IEnumerable<object>>(
            $"{fileName}_Summary.csv", [tradingSim.Summary]));

        return fileData;
    }

    public static byte[] GetZipFromFileData<T>(this IEnumerable<FileData<IEnumerable<T>>> files)
    {
        using var memoryStream = new MemoryStream();

        using (var zipArchive = new ZipArchive(memoryStream, ZipArchiveMode.Create, true))
        {
            foreach (var file in files)
            {
                var zipEntry = zipArchive.CreateEntry(file.FileName, CompressionLevel.Optimal);

                using var entryStream = zipEntry.Open();

                using var fileStream = new MemoryStream(file.Value.GetCsvBytes());

                fileStream.CopyTo(entryStream);
            }
        }

        memoryStream.Seek(0, SeekOrigin.Begin);

        return memoryStream.ToArray();
    }

    public static T[] GetObjectFromCsv<T>(this IFormFile file)
    {
        using var reader = new StreamReader(file.OpenReadStream());

        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

        return csv.GetRecords<T>().ToArray();
    }

    private static (TradeResult[] Result, SimulationSummary Summary) SimulateTrade(IndicatorResult[] indicators,
        int tradeRisk, decimal riskReward, bool updateTrade)
    {
        var length = indicators.Length;

        var openTrades = new List<TradeResult>();

        var closedTrades = new List<TradeResult>();

        for (var i = 0; i < length; i++)
        {
            UpdateUnrealisedPl(indicators[i], openTrades);

            if (indicators[i].Signal != Signal.None && openTrades.Count == 0)
            {
                openTrades.Add(new TradeResult
                {
                    Running = true,
                    StartIndex = i,
                    StartPrice = indicators[i].Signal == Signal.Buy
                        ? indicators[i].Candle.Ask_C
                        : indicators[i].Candle.Bid_C,
                    TriggerPrice = indicators[i].Signal == Signal.Buy
                        ? indicators[i].Candle.Ask_C
                        : indicators[i].Candle.Bid_C,
                    Signal = indicators[i].Signal,
                    TakeProfit = indicators[i].TakeProfit,
                    StopLoss = indicators[i].StopLoss,
                    InitialStopLoss = indicators[i].StopLoss,
                    StartTime = indicators[i].Candle.Time,
                    EndTime = indicators[i].Candle.Time,
                    Result = 0
                });

                continue;
            }

            UpdateTrades(indicators[i], updateTrade, openTrades, closedTrades);

            openTrades.RemoveAll(ot => !ot.Running);
        }

        CloseTradesAtEndOfData(indicators, openTrades, closedTrades);

        var summary = CalcSimSummary(indicators, tradeRisk, riskReward, closedTrades);

        return (closedTrades.ToArray(), summary);
    }

    private static void CloseTradesAtEndOfData(IndicatorResult[] indicators, List<TradeResult> openTrades,
        List<TradeResult> closedTrades)
    {
        var lastIndicator = indicators[^1];

        foreach (var trade in openTrades)
        {
            var exitPrice = trade.Signal == Signal.Buy
                ? lastIndicator.Candle.Bid_C
                : lastIndicator.Candle.Ask_C;

            CloseTrade(trade, GetLossResult(trade, exitPrice), lastIndicator.Candle.Time, exitPrice);

            closedTrades.Add(trade);
        }
    }

    private static (PairTradeResult[] Result, SimulationSummary Summary) SimulatePairsTrade(
        PairsIndicatorResult[] indicators, int tradeRisk)
    {
        var length = indicators.Length;

        var openTrades = new List<PairTradeResult>();

        var closedTrades = new List<PairTradeResult>();

        for (var i = 0; i < length; i++)
        {
            UpdateUnrealisedPlForPairs(indicators[i], openTrades);
            
            if (indicators[i].Signal != Signal.None && openTrades.Count == 0)
            {
                openTrades.Add(new PairTradeResult
                {
                    Running = true,
                    CandleASignal = indicators[i].Signal == Signal.Buy
                        ? Signal.Buy
                        : Signal.Sell,
                    CandleBSignal = indicators[i].Signal == Signal.Buy
                        ? Signal.Sell
                        : Signal.Buy,
                    TriggerAPrice = indicators[i].Signal == Signal.Buy
                        ? indicators[i].CandleA.Ask_C
                        : indicators[i].CandleA.Bid_C,
                    TriggerBPrice = indicators[i].Signal == Signal.Buy
                        ? indicators[i].CandleB.Bid_C
                        : indicators[i].CandleB.Ask_C,
                    UnitsA = indicators[i].UnitsA,
                    UnitsB = indicators[i].UnitsB,
                    StartTime = indicators[i].CandleA.Time,
                    EndTime = indicators[i].CandleA.Time,
                    Result = 0
                });

                continue;
            }

            UpdatePairsTrade(indicators[i], openTrades, closedTrades, tradeRisk);

            openTrades.RemoveAll(ot => !ot.Running);
        }

        ClosePairsTradesAtEndOfData(indicators, openTrades, closedTrades);

        var summary = CalcPairsSimSummary(indicators, tradeRisk, closedTrades);

        return (closedTrades.ToArray(), summary);
    }

    private static void ClosePairsTradesAtEndOfData(PairsIndicatorResult[] indicators,
        List<PairTradeResult> openTrades, List<PairTradeResult> closedTrades)
    {
        var lastIndicator = indicators[^1];

        foreach (var trade in openTrades)
        {
            trade.Running = false;
            trade.EndTime = lastIndicator.CandleA.Time;
            trade.Result = trade.UnrealisedPl > 0 ? 1 : -1;

            closedTrades.Add(trade);
        }
    }

    private static void UpdateUnrealisedPl(IndicatorResult indicator, List<TradeResult> openTrades)
    {
        foreach (var trade in openTrades)
        {
            trade.UnrealisedPl = trade.Signal switch
            {
                Signal.Buy => indicator.Candle.Bid_C - trade.TriggerPrice,
                Signal.Sell => trade.TriggerPrice - indicator.Candle.Ask_C,
                _ => trade.UnrealisedPl
            };
        }
    }
    
    private static void UpdateUnrealisedPlForPairs(PairsIndicatorResult indicator, List<PairTradeResult> openTrades)
    {
        foreach (var trade in openTrades)
        {
            var apl = trade.CandleASignal switch
            {
                Signal.Buy => indicator.CandleA.Bid_C - trade.TriggerAPrice,
                Signal.Sell => trade.TriggerAPrice - indicator.CandleA.Ask_C,
                _ => trade.UnrealisedPl
            };
            
            var bpl = trade.CandleBSignal switch
            {
                Signal.Buy => indicator.CandleB.Bid_C - trade.TriggerBPrice,
                Signal.Sell => trade.TriggerBPrice - indicator.CandleB.Ask_C,
                _ => trade.UnrealisedPl
            };

            trade.UnrealisedPl = apl * trade.UnitsA + bpl * trade.UnitsB;
        }
    }

    private static void UpdateTrades(IndicatorResult indicator, bool updateTrade, List<TradeResult> openTrades,
        List<TradeResult> closedTrades)
    {
        foreach (var trade in openTrades)
        {
            UpdateTrade(trade, indicator);

            if (updateTrade && trade.Running)
            {
                ApplyTrailingStop(trade, indicator);
            }

            if (trade.Running) continue;

            closedTrades.Add(trade);
        }
    }
    
    private static void ApplyTrailingStop(TradeResult trade, IndicatorResult indicator)
    {
        var currentValue = trade.Signal == Signal.Buy
            ? indicator.Candle.Ask_C
            : indicator.Candle.Bid_C;

        if (trade.TrailingDistance is null)
        {
            if (!IsCloserToTakeProfitThanEntry(trade, currentValue)) return;

            trade.TrailingDistance = Math.Abs(currentValue - trade.TriggerPrice) - indicator.Candle.Spread;
        }

        var trailingStop = trade.Signal == Signal.Buy
            ? currentValue - trade.TrailingDistance.Value
            : currentValue + trade.TrailingDistance.Value;

        trade.StopLoss = trade.Signal == Signal.Buy
            ? Math.Max(trade.StopLoss, trailingStop)
            : Math.Min(trade.StopLoss, trailingStop);
    }

    private static bool IsCloserToTakeProfitThanEntry(TradeResult trade, decimal currentValue)
    {
        var distanceToEntry = Math.Abs(currentValue - trade.TriggerPrice);

        var distanceToTakeProfit = Math.Abs(currentValue - trade.TakeProfit);

        return distanceToTakeProfit < distanceToEntry;
    }

    private static void UpdatePairsTrade(PairsIndicatorResult indicator, List<PairTradeResult> openTrades,
        List<PairTradeResult> closedTrades, int tradeRisk)
    {
        var shouldExit = ShouldExitPairsTrade(openTrades, indicator, tradeRisk);

        foreach (var trade in openTrades)
        {
            if (shouldExit)
            {
                trade.Running = false;
                trade.EndTime = indicator.CandleA.Time;
                trade.Result = trade.UnrealisedPl > 0 ? 1 : -1;
            }

            if (trade.Running) continue;

            closedTrades.Add(trade);
        }
    }

    private static void UpdateTrade(TradeResult trade, IndicatorResult indicator)
    {
        if (trade.Signal == Signal.Buy)
        {
            if (indicator.Candle.Bid_H >= trade.TakeProfit && indicator.Candle.Bid_L > trade.StopLoss)
            {
                CloseTrade(trade, 1, indicator.Candle.Time, indicator.Candle.Bid_H);
            }

            if (indicator.Candle.Bid_L <= trade.StopLoss && indicator.Candle.Bid_H < trade.TakeProfit)
            {
                CloseTrade(trade, GetLossResult(trade, trade.StopLoss), indicator.Candle.Time, trade.StopLoss);
            }

            if (indicator.Candle.Bid_L <= trade.StopLoss && indicator.Candle.Bid_H >= trade.TakeProfit)
            {
                CloseTrade(trade, 0, indicator.Candle.Time, indicator.Candle.Mid_C);
            }
        }

        if (trade.Signal == Signal.Sell)
        {
            if (indicator.Candle.Ask_L <= trade.TakeProfit && indicator.Candle.Ask_H < trade.StopLoss)
            {
                CloseTrade(trade, 1, indicator.Candle.Time, indicator.Candle.Ask_L);
            }

            if (indicator.Candle.Ask_H >= trade.StopLoss && indicator.Candle.Ask_L > trade.TakeProfit)
            {
                CloseTrade(trade, GetLossResult(trade, trade.StopLoss), indicator.Candle.Time, trade.StopLoss);
            }

            if (indicator.Candle.Ask_H >= trade.StopLoss && indicator.Candle.Ask_L <= trade.TakeProfit)
            {
                CloseTrade(trade, 0, indicator.Candle.Time, indicator.Candle.Mid_C);
            }
        }
    }
    
    private static int GetLossResult(TradeResult trade, decimal exitPrice)
    {
        var realisedPl = trade.Signal == Signal.Buy
            ? exitPrice - trade.TriggerPrice
            : trade.TriggerPrice - exitPrice;

        if (realisedPl >= 0) return 1;

        return -1;
    }

    private static void CloseTrade(TradeResult trade, decimal result, DateTime endTime, decimal exitPrice)
    {
        trade.Running = false;
        trade.Result = Math.Round(GetAccurateResult(result, trade, exitPrice), 2);
        trade.EndTime = endTime;
        trade.TriggerPrice = exitPrice;
    }

    private static decimal GetAccurateResult(decimal result, TradeResult trade, decimal exitPrice)
    {
        if (result < 0) return -GetDistance(trade.TriggerPrice, exitPrice, trade.InitialStopLoss);

        if (result == 0) return result;

        var reachedTakeProfit = trade.Signal == Signal.Buy
            ? exitPrice >= trade.TakeProfit
            : exitPrice <= trade.TakeProfit;

        return reachedTakeProfit ? 1 : GetDistance(trade.TriggerPrice, exitPrice, trade.TakeProfit);
    }
    
    private static decimal GetDistance(decimal entryPrice, decimal exitPrice, decimal takeProfit)
    {
        var targetDistance = Math.Abs(takeProfit - entryPrice);

        if (targetDistance == 0) return 1;

        var achievedDistance = Math.Abs(exitPrice - entryPrice);

        return Math.Clamp(achievedDistance / targetDistance, 0, 1);
    }

    private static SimulationSummary CalcSimSummary(IndicatorResult[] indicators, int tradeRisk, decimal riskReward,
        List<TradeResult> closedTrades)
    {
        var summary = new SimulationSummary
        {
            Days = indicators.Last().Candle.Time.Subtract(indicators.First().Candle.Time).Days,
            Candles = indicators.Length,
            Trades = closedTrades.Count,
            Wins = closedTrades.Count(t => t.Result > 0),
            Losses = closedTrades.Count(t => t.Result < 0),
            Unknown = closedTrades.Count(t => t.Result == 0),
            TradeRisk = tradeRisk
        };

        summary.WinRate = CalcWinRate(summary.Wins, summary.Trades - summary.Unknown);

        var buyWins = closedTrades.Count(t => t.Result > 0 && t.Signal == Signal.Buy);

        var buyTrades = closedTrades.Count(t => t.Result != 0 && t.Signal == Signal.Buy);

        summary.BuyWinRate = CalcWinRate(buyWins, buyTrades);

        var sellWins = closedTrades.Count(t => t.Result > 0 && t.Signal == Signal.Sell);

        var sellTrades = closedTrades.Count(t => t.Result != 0 && t.Signal == Signal.Sell);

        summary.SellWinRate = CalcWinRate(sellWins, sellTrades);

        var winResultSum = closedTrades.Where(t => t.Result > 0).Sum(t => t.Result);

        var lossResultSum = closedTrades.Where(t => t.Result < 0).Sum(t => t.Result);

        summary.Balance = (double)Math.Round(winResultSum * tradeRisk * riskReward + lossResultSum * tradeRisk, 2);

        return summary;
    }

    private static SimulationSummary CalcPairsSimSummary(PairsIndicatorResult[] indicators, int tradeRisk,
        List<PairTradeResult> closedTrades)
    {
        var summary = new SimulationSummary
        {
            Days = indicators.Last().CandleA.Time.Subtract(indicators.First().CandleA.Time).Days,
            Candles = indicators.Length,
            Trades = closedTrades.Count,
            Wins = closedTrades.Count(t => t.Result == 1),
            Losses = closedTrades.Count(t => t.Result == -1),
            TradeRisk = tradeRisk
        };

        summary.WinRate = CalcWinRate(summary.Wins, summary.Trades - summary.Unknown);

        var winResultSum = closedTrades.Where(t => t.Result == 1).Sum(t => t.Result);

        summary.Balance = (double)Math.Round(winResultSum * tradeRisk - summary.Losses * tradeRisk, 2);

        return summary;
    }

    private static double CalcWinRate(int wins, int decidedTrades)
    {
        return decidedTrades == 0 ? 0 : Math.Round((double)wins * 100 / decidedTrades, 2);
    }

    private static bool ShouldExitPairsTrade(List<PairTradeResult> openTrades, PairsIndicatorResult indicator,
        int tradeRisk)
    {
        if (openTrades.Count == 0) return false;

        var totalPl = openTrades.Sum(ot => ot.UnrealisedPl);

        return CanTakeProfit(indicator, totalPl) ||
               HasOverExposureWithProfit(indicator.CandleA.Time, openTrades) ||
               indicator.StopLoss || totalPl < -tradeRisk;
    }

    private static bool CanTakeProfit(PairsIndicatorResult indicator, decimal totalPl)
    {
        return indicator.TakeProfit && totalPl > 0;
    }

    private static bool HasOverExposureWithProfit(DateTime currentTime, List<PairTradeResult> openTrades)
        => openTrades.Any(ot => currentTime.Subtract(ot.StartTime) >= TimeSpan.FromHours(1)) &&
           openTrades.Sum(ot => ot.UnrealisedPl) > 0;
}