namespace Trading.Bot.API.Mediator;

public sealed class CandlesHandler(OandaApiService apiService) : IRequestHandler<CandlesRequest, IResult>
{
    public async Task<IResult> Handle(CandlesRequest request, CancellationToken cancellationToken)
    {
        if (!request.Currencies.Contains(','))
        {
            return Results.BadRequest("Please provide comma separated currencies");
        }

        var currencyList = request.Currencies.Split(',', StringSplitOptions.RemoveEmptyEntries);

        var instruments = currencyList.GetAllCombinations();

        var granularities = request.Granularity.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (granularities.Length == 0)
        {
            granularities = [OandaApiService.DefaultGranularity];
        }

        var candlesBag = new ConcurrentBag<FileData<IEnumerable<Candle>>>();

        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = 3
        };

        if (!DateTime.TryParse(request.FromDate, out var fromDate)) fromDate = default;

        if (!DateTime.TryParse(request.ToDate, out var toDate)) toDate = default;

        var count = int.TryParse(request.Count, out var parsedCount) ? parsedCount : 500;

        await Parallel.ForEachAsync(instruments, parallelOptions, async (instrument, _) =>
        {
            foreach (var granularity in granularities)
            {
                var candles = (await apiService.GetCandles(
                        instrument, granularity, request.Price, count, fromDate, cancellationToken)).ToList();

                if (candles.Count == 0) continue;

                if (toDate != default && candles.Last().Time < toDate)
                {
                    while (candles.Last().Time < toDate)
                    {
                        candles.AddRange(await apiService.GetCandles(
                            instrument, request.Granularity, request.Price, count, candles.Last().Time, cancellationToken));
                    }

                    if (candles.Last().Time > toDate) candles.RemoveAll(c => c.Time > toDate);

                    candlesBag.Add(new FileData<IEnumerable<Candle>>(
                        $"{instrument}_{granularity}.csv",
                        candles.DistinctBy(c => c.Time)));
                }
                else
                {
                    candlesBag.Add(new FileData<IEnumerable<Candle>>(
                        $"{instrument}_{granularity}.csv", candles));
                }
            }
        });

        return Results.File(candlesBag.GetZipFromFileData(),
            "application/octet-stream", $"{request.Currencies}_{request.Granularity}_Candles.zip");
    }
}

public record CandlesRequest : IHttpRequest
{
    public string Currencies { get; set; } = "";
    public string Granularity { get; set; } = "";
    public string Price { get; set; } = "";
    public string FromDate { get; set; } = "";
    public string ToDate { get; set; } = "";
    public string Count { get; set; } = "";
}