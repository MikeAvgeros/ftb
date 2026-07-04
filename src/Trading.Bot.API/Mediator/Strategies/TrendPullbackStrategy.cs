namespace Trading.Bot.API.Mediator.Strategies;

public sealed class TrendPullbackStrategy : IStrategy
{
    public StrategyType Type => StrategyType.TrendPullback;

    public IResult Run(RunStrategyRequest request)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();
        
        var maxSpread = request.MaxSpread ?? 0.0003m;
        var minGain = request.MinGain ?? 0;
        var riskReward = request.RiskReward ?? 1;
        var tradeRisk = request.TradeRisk ?? 10;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();
            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];
            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var bbWindow = request.GetInt(0, 20);
            var emaWindow = request.GetInt(1, 100);
            var standardDeviation = request.GetDouble(0, 2);

            var bollingerBands = candles.CalcTrendPullback(bbWindow, emaWindow, standardDeviation,
                maxSpread, minGain, riskReward);

            var fileName = $"TrendPullback_{instrument}_{granularity}_{bbWindow}_{emaWindow}_{standardDeviation}";

            fileData.AddRange(bollingerBands.GetFileData(fileName, tradeRisk, riskReward));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "TrendPullback_.zip");
    }
}
