namespace Trading.Bot.API.Mediator;

public class TrendBreakoutHandler : IRequestHandler<TrendBreakoutRequest, IResult>
{
    public Task<IResult> Handle(TrendBreakoutRequest request, CancellationToken cancellationToken)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var maxSpread = request.MaxSpread ?? 0.0003m;

        var riskReward = request.RiskReward ?? 1;

        var tradeRisk = request.TradeRisk ?? 20;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();

            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];

            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var nextCandle = candles.CalcZScorePullBack();

            var fileName = $"TrendBreakout_{instrument}_{granularity}";

            fileData.AddRange(nextCandle.GetFileData(fileName, tradeRisk, riskReward));
        }

        if (fileData.Count == 0) return Task.FromResult(Results.Empty);

        return Task.FromResult(Results.File(fileData.GetZipFromFileData(),
            "application/octet-stream", "TrendBreakout.zip"));
    }
}

public record TrendBreakoutRequest : IHttpRequest
{
    public IFormFileCollection Files { get; set; } = new FormFileCollection();
    public int Window { get; set; }
    public decimal? MaxSpread { get; set; }
    public decimal? RiskReward { get; set; }
    public int? TradeRisk { get; set; }
}