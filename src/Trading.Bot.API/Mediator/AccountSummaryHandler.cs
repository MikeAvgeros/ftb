namespace Trading.Bot.API.Mediator;

public sealed class AccountSummaryHandler(OandaApiService apiService) : IRequestHandler<AccountSummaryRequest, IResult>
{
    public async Task<IResult> Handle(AccountSummaryRequest request, CancellationToken cancellationToken)
    {
        var apiResponse = await apiService.GetAccountSummary(cancellationToken);

        if (apiResponse is null) return Results.Empty;

        var bytes = new List<AccountResponse> { apiResponse }.GetCsvBytes();

        return request.Download
            ? Results.File(bytes, "text/csv", "account.csv")
            : Results.Ok(apiResponse);
    }
}

public record AccountSummaryRequest : IHttpRequest
{
    public bool Download { get; set; }
}