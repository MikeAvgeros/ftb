namespace Trading.Bot.API.Mediator.Strategies;

public sealed class MikeStrategy : IStrategy
{
    public StrategyType Type => StrategyType.MikeStrategy;

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

            var shortWindow = request.GetInt(0, 20);
            var longWindow = request.GetInt(1, 50);
            var stdDev = request.GetDouble(1, 1.5);

            var nextCandle = candles.CalcMikeStrategy(shortWindow, longWindow, stdDev, maxSpread,
                minGain, riskReward);

            var fileName = $"MikeStrategy_{instrument}_{granularity}";

            fileData.AddRange(nextCandle.GetFileData(fileName, tradeRisk, riskReward, updateTrade));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "Mike_Strategy.zip");
    }
}
