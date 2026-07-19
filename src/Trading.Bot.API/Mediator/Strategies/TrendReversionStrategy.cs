namespace Trading.Bot.API.Mediator.Strategies;

public sealed class TrendReversionStrategy : IStrategy
{
    public StrategyType Type => StrategyType.TrendReversion;

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
            var stdDev = request.GetDouble(0, 2);

            var nextCandle = candles.CalcTrendReversion(shortWindow, mediumWindow, longWindow,
                stdDev, maxSpread, minGain, riskReward);

            var fileName = $"TrendReversion_{instrument}_{granularity}";

            fileData.AddRange(nextCandle.GetFileData(fileName, tradeRisk, riskReward, updateTrade));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "TrendReversion.zip");
    }
}
