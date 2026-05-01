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

                var fileStream = new MemoryStream(file.Value.GetCsvBytes());

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
                    StartTime = indicators[i].Candle.Time,
                    EndTime = indicators[i].Candle.Time,
                    Result = 0
                });

                continue;
            }

            UpdateTrades(indicators[i], updateTrade, openTrades, closedTrades);

            openTrades.RemoveAll(ot => !ot.Running);
        }

        var summary = CalcSimSummary(indicators, tradeRisk, riskReward, closedTrades);

        return (closedTrades.ToArray(), summary);
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
                    StartTime = indicators[i].CandleA.Time,
                    EndTime = indicators[i].CandleA.Time,
                    Result = 0
                });

                continue;
            }

            UpdatePairsTrade(indicators[i], openTrades, closedTrades, tradeRisk);

            openTrades.RemoveAll(ot => !ot.Running);
        }

        var summary = CalcPairsSimSummary(indicators, tradeRisk, closedTrades);

        return (closedTrades.ToArray(), summary);
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
            
            trade.UnrealisedPl = apl + bpl;
        }
    }

    private static void UpdateTrades(IndicatorResult indicator, bool updateTrade, List<TradeResult> openTrades,
        List<TradeResult> closedTrades)
    {
        foreach (var trade in openTrades)
        {
            UpdateTrade(trade, indicator);

            if (ShouldUpdateStopLoss(updateTrade, trade, indicator))
            {
                trade.StopLoss = trade.Signal == Signal.Buy
                    ? trade.TriggerPrice + indicator.Candle.Spread
                    : trade.TriggerPrice - indicator.Candle.Spread;
            }

            if (trade.Running) continue;

            closedTrades.Add(trade);
        }
    }

    private static void UpdatePairsTrade(PairsIndicatorResult indicator, List<PairTradeResult> openTrades,
        List<PairTradeResult> closedTrades, int tradeRisk)
    {
        foreach (var trade in openTrades)
        {
            if (ShouldExitPairsTrade(openTrades, indicator, tradeRisk))
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
                CloseTrade(trade, GetLossResult(trade), indicator.Candle.Time, indicator.Candle.Bid_L);
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
                CloseTrade(trade, GetLossResult(trade), indicator.Candle.Time, indicator.Candle.Ask_H);
            }

            if (indicator.Candle.Ask_H >= trade.StopLoss && indicator.Candle.Ask_L <= trade.TakeProfit)
            {
                CloseTrade(trade, 0, indicator.Candle.Time, indicator.Candle.Mid_C);
            }
        }
    }

    private static bool ShouldUpdateStopLoss(bool updateTrade, TradeResult trade, IndicatorResult indicator)
    {
        var priceList = new List<decimal> { trade.TriggerPrice, trade.TakeProfit };

        var currentValue = trade.Signal == Signal.Buy
            ? indicator.Candle.Ask_C
            : indicator.Candle.Bid_C;

        var closest = priceList.OrderBy(value => Math.Abs(currentValue - value)).First();

        var currentStop = trade.Signal == Signal.Buy
            ? trade.StopLoss - trade.TriggerPrice
            : trade.TriggerPrice - trade.StopLoss;


        return updateTrade && trade.Running && currentStop < 0 && trade.TakeProfit - closest == 0;
    }

    private static int GetLossResult(TradeResult trade)
    {
        var stoppedPrice = trade.Signal == Signal.Buy
            ? trade.StopLoss - trade.TriggerPrice
            : trade.TriggerPrice - trade.StopLoss;

        if (stoppedPrice >= 0) return 1;

        return -1;
    }

    private static void CloseTrade(TradeResult trade, decimal result, DateTime endTime, decimal triggerPrice)
    {
        trade.Running = false;
        trade.Result = Math.Round(GetAccurateResult(result, trade, triggerPrice), 2);
        trade.EndTime = endTime;
        trade.TriggerPrice = triggerPrice;
    }

    private static decimal GetAccurateResult(decimal result, TradeResult trade, decimal triggerPrice)
    {
        if (result <= 0) return result;

        return trade.Signal switch
        {
            Signal.Buy => triggerPrice >= trade.TakeProfit ? 1 : GetDistance(triggerPrice, trade.TakeProfit),
            Signal.Sell => triggerPrice <= trade.TakeProfit ? 1 : GetDistance(triggerPrice, trade.TakeProfit),
            _ => result
        };
    }

    private static decimal GetDistance(decimal triggerPrice, decimal takeProfit)
    {
        return Math.Abs(triggerPrice - takeProfit) / ((triggerPrice + takeProfit) / 2) * 100;
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

        summary.WinRate = Math.Round((double)summary.Wins * 100 / (summary.Trades - summary.Unknown), 2);

        var buyWins = closedTrades.Count(t => t.Result > 0 && t.Signal == Signal.Buy);

        var buyTrades = closedTrades.Count(t => t.Result != 0 && t.Signal == Signal.Buy);

        summary.BuyWinRate = Math.Round((double)buyWins * 100 / buyTrades, 2);

        var sellWins = closedTrades.Count(t => t.Result > 0 && t.Signal == Signal.Sell);

        var sellTrades = closedTrades.Count(t => t.Result != 0 && t.Signal == Signal.Sell);

        summary.SellWinRate = Math.Round((double)sellWins * 100 / sellTrades, 2);

        var winResultSum = Math.Round(closedTrades.Where(t => t.Result > 0).Sum(t => t.Result));

        summary.Balance = (double)Math.Round(winResultSum * tradeRisk * riskReward - summary.Losses * tradeRisk, 2);

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

        summary.WinRate = Math.Round((double)summary.Wins * 100 / (summary.Trades - summary.Unknown), 2);

        var winResultSum = Math.Round(closedTrades.Where(t => t.Result == 1).Sum(t => t.Result));

        summary.Balance = (double)Math.Round(winResultSum * tradeRisk - summary.Losses * tradeRisk, 2);

        return summary;
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