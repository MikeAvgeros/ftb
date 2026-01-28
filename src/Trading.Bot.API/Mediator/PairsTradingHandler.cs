namespace Trading.Bot.API.Mediator;

public class PairsTradingHandler : IRequestHandler<PairsTradingRequest, IResult>
{
    public Task<IResult> Handle(PairsTradingRequest request, CancellationToken cancellationToken)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var tradeRisk = request.TradeRisk ?? 20;

        var maxSpread = request.MaxSpread ?? 0.0004m;

        if (request.Files.Count != 2) throw new ArgumentException("Strategy works with 2 pairs.");

        var pairA = request.Files[0].GetObjectFromCsv<Candle>();

        var pairB = request.Files[1].GetObjectFromCsv<Candle>();

        if (pairA.Length == 0 || pairB.Length == 0) throw new ArgumentException("Candles are required.");

        var result = pairA.CalcRatioZScore(pairB, request.Window, maxSpread);

        var instruments = string.Join("",
            request.Files[0].FileName[..request.Files[0].FileName.LastIndexOf('_')].Concat(
                request.Files[1].FileName[..request.Files[1].FileName.LastIndexOf('_')]));

        var granularity = request.Files[0]
            .FileName[(request.Files[0].FileName.LastIndexOf('_') + 1)..request.Files[0].FileName.IndexOf('.')];

        var fileName = $"PairsTrading_{instruments}_{granularity}";

        fileData.AddRange(result.GetFileData(fileName, tradeRisk));

        return Task.FromResult(Results.File(fileData.GetZipFromFileData(),
            "application/octet-stream", "PairsTrading.zip"));
    }
}

public record PairsTradingRequest : IHttpRequest
{
    public IFormFileCollection Files { get; set; } = new FormFileCollection();
    public int Window { get; set; }
    public int? TradeRisk { get; set; }
    public decimal? MaxSpread { get; set; }
}