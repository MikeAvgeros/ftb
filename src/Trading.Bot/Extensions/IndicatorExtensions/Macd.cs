namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static MacdResult[] CalcMacd(this Candle[] candles, int shortWindow = 12, int longWindow = 26, int signal = 9)
    {
        var length = candles.Length;

        var result = new MacdResult[length];

        Span<double> prices = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];

        for (var i = 0; i < length; i++)
        {
            prices[i] = (double)candles[i].Mid_C;
        }

        var emaShort = prices.CalcEma(shortWindow);

        var emaLong = prices.CalcEma(longWindow);

        Span<double> macd = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];

        for (var i = 0; i < length; i++)
        {
            var value = emaShort[i] - emaLong[i];

            macd[i] = value;

            result[i] = new MacdResult
            {
                Candle = candles[i],
                Macd = value
            };
        }

        var ema = macd.CalcEma(signal);
        
        var warmupPeriod = longWindow + signal - 2;

        for (var i = 0; i < length; i++)
        {
            if (i < warmupPeriod)
            {
                result[i].Macd = 0.0;
                
                result[i].SignalLine = 0.0;
                
                result[i].Histogram = 0.0;
                continue;
            }
            
            result[i].SignalLine = ema[i];

            result[i].Histogram = result[i].Macd - ema[i];
        }

        return result;
    }
}