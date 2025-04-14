namespace Trading.Bot.API.Mediator;

public class TrendReversionHandler : IRequestHandler<TrendReversionRequest, IResult>
{
    public Task<IResult> Handle(TrendReversionRequest request, CancellationToken cancellationToken)
    {
        var fileData = new List<FileData<IEnumerable<object>>>();

        var maxSpread = request.MaxSpread ?? 0.0004m;

        var minGain = request.MinGain ?? 0.001m;

        var riskReward = request.RiskReward ?? 1;

        var tradeRisk = request.TradeRisk ?? 20;

        foreach (var file in request.Files)
        {
            var candles = file.GetObjectFromCsv<Candle>();

            if (candles.Length == 0) continue;

            var instrument = file.FileName[..file.FileName.LastIndexOf('_')];

            var granularity = file.FileName[(file.FileName.LastIndexOf('_') + 1)..file.FileName.IndexOf('.')];

            var nextCandle = candles.CalcTrendReversion(request.ShortWindow, request.MedWindow, request.LongWindow,
                request.StdDev, maxSpread, minGain, riskReward);

            var fileName = $"TrendReversion_{instrument}_{granularity}";

            fileData.AddRange(nextCandle.GetFileData(fileName, tradeRisk, riskReward, true));
        }

        if (fileData.Count == 0) return Task.FromResult(Results.Empty);

        return Task.FromResult(Results.File(fileData.GetZipFromFileData(),
            "application/octet-stream", "TrendReversion.zip"));
    }
}

public record TrendReversionRequest : IHttpRequest
{
    public IFormFileCollection Files { get; set; } = new FormFileCollection();
    public int ShortWindow { get; set; }
    public int MedWindow { get; set; }
    public int LongWindow { get; set; }
    public double StdDev { get; set; }
    public decimal? MaxSpread { get; set; }
    public decimal? MinGain { get; set; }
    public decimal? RiskReward { get; set; }
    public int? TradeRisk { get; set; }
}