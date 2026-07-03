namespace Trading.Bot.API.Mediator;

public sealed class InstrumentsHandler(OandaApiService apiService) : IRequestHandler<InstrumentsRequest, IResult>
{
    public async Task<IResult> Handle(InstrumentsRequest request, CancellationToken cancellationToken)
    {
        var instrumentList = (await apiService.GetInstruments(request.Instruments, cancellationToken)).ToList();

        if (!string.IsNullOrEmpty(request.Type))
        {
            instrumentList.RemoveAll(i =>
                !string.Equals(i.Type, request.Type, StringComparison.OrdinalIgnoreCase));
        }

        if (instrumentList.Count == 0) return Results.Empty;

        return request.Download
            ? Results.File(instrumentList.GetCsvBytes(),
                "text/csv", $"{request.Instruments}_Instruments.csv")
            : Results.Ok(instrumentList);
    }
}

public record InstrumentsRequest : IHttpRequest
{
    public string Instruments { get; set; } = "";
    public string Type { get; set; } = "";
    public bool Download { get; set; }
}