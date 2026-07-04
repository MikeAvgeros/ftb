namespace Trading.Bot.API.Mediator.Strategies;

public sealed class MeanReversionStrategy : IStrategy
{
    public StrategyType Type => StrategyType.MeanReversion;

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

            var window = request.GetInt(0, 20);
            var standardDeviation = request.GetDouble(0, 2);
            var rsiLow = request.GetDouble(1, 30);
            var rsiHigh = request.GetDouble(2, 70);

            var bollingerBands = candles.CalcMeanReversion(window, standardDeviation, rsiLow, rsiHigh, maxSpread,
                minGain, riskReward);

            var fileName = $"MeanReversion_{instrument}_{granularity}_{window}_{standardDeviation}";

            fileData.AddRange(bollingerBands.GetFileData(fileName, tradeRisk, riskReward, true));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "MeanReversion.zip");
    }
}
