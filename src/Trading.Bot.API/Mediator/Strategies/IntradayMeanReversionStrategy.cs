namespace Trading.Bot.API.Mediator.Strategies;

public sealed class IntradayMeanReversionStrategy : IStrategy
{
    public StrategyType Type => StrategyType.IntradayMeanReversion;

    public IResult Run(RunStrategyRequest request)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var maxSpread = request.MaxSpread ?? 0.0004m;
        var minGain = request.MinGain ?? 0.0006m;
        var riskReward = request.RiskReward ?? 1m;
        var tradeRisk = request.TradeRisk ?? 10;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();
            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];
            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var window = request.GetInt(0, 20);
            var rsiWindow = request.GetInt(1, 14);
            var stochWindow = request.GetInt(2, 14);
            var atrWindow = request.GetInt(3, 14);

            var stdDev = request.GetDouble(0, 2);
            var rsiLower = request.GetDouble(1, 20);
            var rsiUpper = request.GetDouble(2, 80);
            var stochLower = request.GetDouble(3, 20);
            var stochUpper = request.GetDouble(4, 80);
            var atrMultiplier = request.GetDouble(5, 1.5);
            var volatilityRegimeMax = request.GetDouble(6, 1.4);

            var intradayMeanReversion = candles.CalcIntradayMeanReversion(window, stdDev, rsiWindow, rsiLower,
                rsiUpper, stochWindow, stochLower, stochUpper, atrWindow, atrMultiplier, volatilityRegimeMax, maxSpread, minGain, riskReward);

            var fileName = $"IntradayMeanReversion_{instrument}_{granularity}_{window}_{stdDev}";

            fileData.AddRange(intradayMeanReversion.GetFileData(fileName, tradeRisk, riskReward, true));
        }

        return fileData.Count == 0
            ? Results.Empty
            : Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "IntradayMeanReversion.zip");
    }
}
