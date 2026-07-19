namespace Trading.Bot.API.Mediator.Strategies;

public sealed class TrendConfluenceStrategy : IStrategy
{
    public StrategyType Type => StrategyType.TrendConfluence;

    public IResult Run(RunStrategyRequest request)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var maxSpread = request.MaxSpread ?? 0.0004m;
        var minGain = request.MinGain ?? 0.001m;
        var riskReward = request.RiskReward ?? 1;
        var tradeRisk = request.TradeRisk ?? 10;
        var updateTrade = request.UpdateTrade ?? true;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();
            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];
            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var shortWindow = request.GetInt(0, 9);
            var mediumWindow = request.GetInt(1, 21);
            var longWindow = request.GetInt(2, 50);
            var atrWindow = request.GetInt(3, 50);
            var rsiLow = request.GetDouble(0, 20);
            var rsiHigh = request.GetDouble(1, 80);
            var atrRatio = request.GetDouble(2, 0.8);

            var nextCandle = candles.CalcTrendConfluence(shortWindow, mediumWindow, longWindow,
                atrWindow, rsiLow, rsiHigh, atrRatio, maxSpread, minGain, riskReward);

            var fileName = $"TrendConfluence{instrument}_{granularity}";

            fileData.AddRange(nextCandle.GetFileData(fileName, tradeRisk, riskReward, updateTrade));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "TrendConfluence.zip");
    }
}
