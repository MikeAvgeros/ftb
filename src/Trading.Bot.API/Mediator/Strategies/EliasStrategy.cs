namespace Trading.Bot.API.Mediator.Strategies;

public sealed class EliasStrategy : IStrategy
{
    public StrategyType Type => StrategyType.EliasStrategy;

    public IResult Run(RunStrategyRequest request)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var minGain = request.MinGain ?? 0.001m;
        var riskReward = request.RiskReward ?? 1;
        var maxSpread = request.MaxSpread ?? 0.0004m;
        var tradeRisk = request.TradeRisk ?? 10;
        var updateTrade = request.UpdateTrade ?? true;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();
            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];
            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var emaShort = request.GetInt(0, 8);
            var emaMedium = request.GetInt(1, 21);
            var smaLong = request.GetInt(2, 50);
            var emaLong = request.GetInt(3, 100);

            var macdEma = candles.CalcEliasStrategy(emaShort, emaMedium, smaLong,
                emaLong, minGain, riskReward, maxSpread);

            var fileName = $"EliasStrategy_{instrument}_{granularity}";

            fileData.AddRange(macdEma.GetFileData(fileName, tradeRisk, riskReward, updateTrade));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "Elias_Strategy.zip");
    }
}
