namespace Trading.Bot.API.Mediator.Strategies;

public sealed class MaCrossStrategy : IStrategy
{
    public StrategyType Type => StrategyType.MaCross;

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

            var windows = request.Ints is { Length: > 1 } ? request.Ints : [10, 20];
            var mergedWindows = windows.GetAllWindowCombinations().Distinct();

            foreach (var window in mergedWindows)
            {
                var movingAvgCross = candles.CalcMaCross(window.Item1, window.Item2, maxSpread, minGain, riskReward);

                var fileName = $"MaCross_{instrument}_{granularity}_{window.Item1}_{window.Item2}";

                fileData.AddRange(movingAvgCross.Cast<IndicatorResult>().ToArray()
                    .GetFileData(fileName, tradeRisk, riskReward, updateTrade));
            }
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "MaCross.zip");
    }
}
