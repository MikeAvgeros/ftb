namespace Trading.Bot.Services;

public class OandaApiService(HttpClient httpClient, ILogger<OandaApiService> logger, Constants constants)
{
    private readonly string _accountId = constants.AccountId;

    public const string DefaultGranularity = "H1";
    private const string DefaultPrice = "MBA";

    private static readonly JsonSerializerOptions ReadJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };
    private static readonly JsonSerializerOptions WriteJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        NumberHandling = JsonNumberHandling.WriteAsString,
        WriteIndented = true
    };

    private async Task<ApiResponse<T>> SendAsync<T>(HttpMethod method, string endpoint, object body = null,
        string dataKey = null, CancellationToken cancellationToken = default) where T : class
    {
        try
        {
            using var request = new HttpRequestMessage(method, endpoint);

            if (method != HttpMethod.Get && body is not null)
            {
                request.Content = Serialize(body);
            }

            using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                return await HandleApiResponse<T>(dataKey, response, cancellationToken);
            }

            var stringResponse = await response.Content.ReadAsStringAsync(cancellationToken);

            logger.LogWarning("{Method} {StatusCode} response from Oanda API.\r\n{StringResponse}", method.Method,
                (int)response.StatusCode, stringResponse);

            return new ApiResponse<T>(response.StatusCode, null);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while sending {Method} to {Endpoint}.\r\n{Body}", method.Method,
                endpoint, body is null ? null : JsonSerializer.Serialize(body, WriteJsonOptions));

            return new ApiResponse<T>(HttpStatusCode.InternalServerError, null);
        }
    }

    private static StringContent Serialize(object body)
    {
        return new StringContent(JsonSerializer.Serialize(body, WriteJsonOptions), Encoding.UTF8,
            "application/json");
    }

    private static async Task<ApiResponse<T>> HandleApiResponse<T>(string dataKey, HttpResponseMessage response,
        CancellationToken cancellationToken) where T : class
    {
        await using var responseStream = await response.Content.ReadAsStreamAsync(cancellationToken);

        if (dataKey == null)
        {
            var value = await JsonSerializer.DeserializeAsync<T>(responseStream, ReadJsonOptions, cancellationToken);

            return new ApiResponse<T>(response.StatusCode, value);
        }

        using var document = await JsonDocument.ParseAsync(responseStream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty(dataKey, out var jsonElement))
            return new ApiResponse<T>(HttpStatusCode.NotFound, null);

        var selectedValue = jsonElement.Deserialize<T>(ReadJsonOptions);

        return new ApiResponse<T>(response.StatusCode, selectedValue);
    }

    public async Task<AccountResponse> GetAccountSummary(CancellationToken cancellationToken = default)
    {
        var endpoint = $"accounts/{_accountId}/summary";

        var response = await SendAsync<AccountResponse>(HttpMethod.Get, endpoint, dataKey: "account",
            cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode()
            ? response.Value
            : null;
    }

    public async Task<Price[]> GetPrices(string instruments, CancellationToken cancellationToken = default)
    {
        var endpoint = $"accounts/{_accountId}/pricing?instruments={instruments}&includeHomeConversions=true";

        var response = await SendAsync<PricingResponse>(HttpMethod.Get, endpoint, cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode() && response.Value is not null
            ? response.Value.MapToPrices()
            : [];
    }

    public async Task<Instrument[]> GetInstruments(string instruments, CancellationToken cancellationToken = default)
    {
        var endpoint = BuildInstrumentsEndpoint(instruments);

        var response = await SendAsync<InstrumentResponse[]>(HttpMethod.Get, endpoint, dataKey: "instruments",
            cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode() && response.Value is not null
            ? response.Value.MapToInstruments()
            : [];
    }

    public async Task<Candle[]> GetCandles(string instrument, string granularity = null,
        string price = null, int count = 500, DateTime fromDate = default, DateTime toDate = default,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildCandlesEndpoint(instrument, granularity, price, count, fromDate, toDate);

        var response = await SendAsync<CandleResponse>(HttpMethod.Get, endpoint, cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode() && response.Value?.Candles is not null
            ? response.Value.Candles.MapToCandles()
            : [];
    }

    public async Task<DateTime> GetLastCandleTime(string instrument, string granularity = null,
        CancellationToken cancellationToken = default)
    {
        var endpoint = BuildCandlesEndpoint(instrument, granularity, count: 1);

        var response = await SendAsync<CandleResponse>(HttpMethod.Get, endpoint, cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode() && response.Value?.Candles is { Length: > 0 } candles
            ? candles[^1].Time
            : default;
    }

    public async Task<OrderFilledResponse> PlaceTrade(Order order, CancellationToken cancellationToken = default)
    {
        var endpoint = $"accounts/{_accountId}/orders";

        var orderRequest = new OrderRequest(order);

        var response = await SendAsync<OrderFilledResponse>(HttpMethod.Post, endpoint, orderRequest,
            "orderFillTransaction", cancellationToken);

        return response.StatusCode.IsSuccessStatusCode()
            ? response.Value
            : null;
    }

    public async Task<bool> UpdateTrade(OrderUpdate update, string tradeId, CancellationToken cancellationToken = default)
    {
        var endpoint = $"accounts/{_accountId}/trades/{tradeId}/orders";

        var response = await SendAsync<OrderUpdatedResponse>(HttpMethod.Put, endpoint, update,
            cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode() && response.Value is not null;
    }

    public async Task<bool> CloseTrade(string tradeId, CancellationToken cancellationToken = default)
    {
        var endpoint = $"accounts/{_accountId}/trades/{tradeId}/close";

        var response = await SendAsync<OrderFilledResponse>(HttpMethod.Put, endpoint, dataKey: "orderFillTransaction",
            cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode() && response.Value is not null;
    }

    public async Task<TradeResponse[]> GetOpenTrades(CancellationToken cancellationToken = default)
    {
        var endpoint = $"accounts/{_accountId}/openTrades";

        var response = await SendAsync<TradeResponse[]>(HttpMethod.Get, endpoint, dataKey: "trades",
            cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode() && response.Value is not null
            ? response.Value
            : [];
    }

    public async Task<TradeResponse> GetTrade(string tradeId, CancellationToken cancellationToken = default)
    {
        var endpoint = $"accounts/{_accountId}/trades/{tradeId}";

        var response = await SendAsync<TradeResponse>(HttpMethod.Get, endpoint, dataKey: "trade",
            cancellationToken: cancellationToken);

        return response.StatusCode.IsSuccessStatusCode()
            ? response.Value
            : null;
    }

    private string BuildInstrumentsEndpoint(string instruments)
    {
        var endpoint = $"accounts/{_accountId}/instruments";

        if (!string.IsNullOrEmpty(instruments))
        {
            endpoint += $"?instruments={instruments}";
        }

        return endpoint;
    }

    private static string BuildCandlesEndpoint(string instrument, string granularity = null,
        string price = null, int count = 500, DateTime fromDate = default, DateTime toDate = default)
    {
        var candleGranularity = string.IsNullOrEmpty(granularity) ? DefaultGranularity : granularity;

        var candlePrice = string.IsNullOrEmpty(price) ? DefaultPrice : price;

        var endpoint =
            $"instruments/{instrument}/candles?granularity={candleGranularity}&price={candlePrice}";

        if (fromDate != default && toDate != default)
        {
            var from = Uri.EscapeDataString(fromDate.ToUniversalTime().ToString("o"));
            var to = Uri.EscapeDataString(toDate.ToUniversalTime().ToString("o"));
            endpoint += $"&from={from}&to={to}";
        }
        else
        {
            endpoint += $"&count={count}";
        }

        return endpoint;
    }
}