namespace Trading.Bot.Services;

public class OandaStreamService(
    ILogger<OandaStreamService> logger,
    HttpClient httpClient,
    LiveTradeCache liveTradeCache,
    Constants constants)
{
    private readonly string _accountId = constants.AccountId;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    public async Task StreamLivePrices(string instruments, CancellationToken stoppingToken)
    {
        var endpoint = $"accounts/{_accountId}/pricing/stream?instruments={instruments}";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            using var response =
                await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, stoppingToken);

            response.EnsureSuccessStatusCode();

            await using var responseStream = await response.Content.ReadAsStreamAsync(stoppingToken);

            await ProcessStreamAsync(PipeReader.Create(responseStream), stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        catch (HttpRequestException ex)
        {
            logger.LogError(ex, "HTTP error connecting to price stream");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Price stream failed unexpectedly");
        }
    }

    private async Task ProcessStreamAsync(PipeReader reader, CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var result = await reader.ReadAsync(ct);
                
                var buffer = result.Buffer;

                while (TryReadLine(ref buffer, out var line))
                {
                    if (!line.IsEmpty)
                        ProcessLine(line);
                }
                
                reader.AdvanceTo(buffer.Start, buffer.End);

                if (result.IsCompleted)
                    break;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        finally
        {
            await reader.CompleteAsync();
        }
    }
    
    private static bool TryReadLine(ref ReadOnlySequence<byte> buffer, out ReadOnlySequence<byte> line)
    {
        var newlinePos = buffer.PositionOf((byte)'\n');

        if (newlinePos is null)
        {
            line = default;
            return false;
        }

        line = buffer.Slice(0, newlinePos.Value);
        
        buffer = buffer.Slice(buffer.GetPosition(1, newlinePos.Value));
        
        return true;
    }

    private void ProcessLine(ReadOnlySequence<byte> line)
    {
        try
        {
            PriceResponse price;

            if (line.IsSingleSegment)
            {
                price = JsonSerializer.Deserialize<PriceResponse>(
                    line.FirstSpan.TrimEnd((byte)'\r'), JsonOptions);
            }
            else
            {
                var rentedBuffer = ArrayPool<byte>.Shared.Rent((int)line.Length);
                
                try
                {
                    line.CopyTo(rentedBuffer);
                    
                    price = JsonSerializer.Deserialize<PriceResponse>(
                        rentedBuffer.AsSpan(0, (int)line.Length).TrimEnd((byte)'\r'), JsonOptions);
                }
                finally
                {
                    ArrayPool<byte>.Shared.Return(rentedBuffer);
                }
            }

            if (price is null || price.Type != "PRICE") return;

            if (price.Tradeable && price.Bids is { Length: > 0 } && price.Asks is { Length: > 0 })
                liveTradeCache.LivePrices[price.Instrument] = new LivePrice(price);
            else
                liveTradeCache.LivePrices.TryRemove(price.Instrument, out _);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Discarding malformed price message, stream continues");
        }
    }
}