namespace Trading.Bot.API.Mediator.Strategies;

public sealed class PairsTradingStrategy : IStrategy
{
    public StrategyType Type => StrategyType.PairsTrading;

    public IResult Run(RunStrategyRequest request)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();
        
        var maxSpread = request.MaxSpread ?? 0.0004m;
        var tradeRisk = request.TradeRisk ?? 10;

        if (request.Files.Count != 2) throw new ArgumentException("Strategy works with 2 pairs.");

        var pairA = request.Files[0].GetObjectFromCsv<Candle>();
        var pairB = request.Files[1].GetObjectFromCsv<Candle>();

        if (pairA.Length == 0 || pairB.Length == 0) throw new ArgumentException("Candles are required.");
        
        var window = request.GetInt(0, 50);

        var result = pairA.CalcReturnSpreadZScore(pairB, window, maxSpread);

        var instruments = string.Join("",
            request.Files[0].FileName[..request.Files[0].FileName.LastIndexOf('_')].Concat(
                request.Files[1].FileName[..request.Files[1].FileName.LastIndexOf('_')]));

        var granularity = request.Files[0]
            .FileName[(request.Files[0].FileName.LastIndexOf('_') + 1)..request.Files[0].FileName.IndexOf('.')];

        var fileName = $"PairsTrading_{instruments}_{granularity}";

        fileData.AddRange(result.GetFileData(fileName, tradeRisk));

        return Results.File(fileData.GetZipFromFileData(), "application/octet-stream", "PairsTrading.zip");
    }
}
