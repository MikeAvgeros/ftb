namespace Trading.Bot.API.Mediator.Strategies;

public sealed class MacdEmaStrategy : IStrategy
{
    public StrategyType Type => StrategyType.MacdEma;

    public IResult Run(RunStrategyRequest request)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var maxSpread = request.MaxSpread ?? 0.0003m;
        var minGain = request.MinGain ?? 0.0006m;
        var riskReward = request.RiskReward ?? 1;
        var tradeRisk = request.TradeRisk ?? 10;
        var updateTrade = request.UpdateTrade ?? true;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();
            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];
            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var emaWindow = request.GetInt(0, 100);

            var macdEma = candles.CalcMacdEma(emaWindow, maxSpread, minGain, riskReward);

            var fileName = $"MacdEma_{instrument}_{granularity}_{emaWindow}";

            fileData.AddRange(macdEma.GetFileData(fileName, tradeRisk, riskReward, updateTrade));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "MacdEma.zip");
    }
}
