namespace Trading.Bot.Extensions;

public static class ApiServiceMapperExtensions
{
    public static Candle[] MapToCandles(this CandleData[] candles)
    {
        var length = candles.Count(c => c.Complete);

        var result = new Candle[length];
        var j = 0;

        for (var i = 0; i < candles.Length; i++)
        {
            if (!candles[i].Complete) continue;

            result[j++] = new Candle(candles[i]);
        }

        return result;
    }

    public static Instrument[] MapToInstruments(this InstrumentResponse[] instruments)
    {
        var length = instruments.Length;

        var result = new Instrument[length];

        for (var i = 0; i < length; i++)
        {
            result[i] = new Instrument(instruments[i]);
        }

        return result;
    }

    public static Price[] MapToPrices(this PricingResponse pricingResponse)
    {
        if (pricingResponse.Prices is not { Length: > 0 }) return [];

        var homeConversions = pricingResponse.HomeConversions ?? [];
        var length = pricingResponse.Prices.Length;
        var result = new Price[length];

        for (var i = 0; i < length; i++)
        {
            var parts = pricingResponse.Prices[i].Instrument.Split('_');
            var baseInstrument = parts.Length > 1 ? parts[1] : parts[0];
            var conversion = homeConversions.FirstOrDefault(c => c.Currency == baseInstrument);

            result[i] = new Price(pricingResponse.Prices[i], conversion);
        }

        return result;
    }
}