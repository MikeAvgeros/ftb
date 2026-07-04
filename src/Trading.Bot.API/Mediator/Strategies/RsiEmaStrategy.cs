namespace Trading.Bot.API.Mediator.Strategies;

public sealed class RsiEmaStrategy : IStrategy
{
    public StrategyType Type => StrategyType.RsiEma;

    public IResult Run(RunStrategyRequest request)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();
        
        var maxSpread = request.MaxSpread ?? 0.0003m;
        var minGain = request.MinGain ?? 0.0006m;
        var riskReward = request.RiskReward ?? 1;
        var tradeRisk = request.TradeRisk ?? 10;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();
            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];
            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var rsiWindow = request.GetInt(0, 14);
            var emaWindow = request.GetInt(1, 200);
            var rsiLimit = request.GetDouble(0, 50);

            var rsi = candles.CalcRsiEma(rsiWindow, emaWindow, rsiLimit, maxSpread,
                minGain, riskReward);

            var fileName = $"RsiEma_{instrument}_{granularity}_{rsiWindow}_{emaWindow}";

            fileData.AddRange(rsi.GetFileData(fileName, tradeRisk, riskReward, true));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "RsiEma.zip");
    }
}
