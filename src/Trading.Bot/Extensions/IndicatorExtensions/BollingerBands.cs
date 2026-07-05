namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static BollingerBandsResult[] CalcBollingerBands(this Candle[] candles, int window = 20, double stdDev = 2)
    {
        var length = candles.Length;

        var result = new BollingerBandsResult[length];
        
        if (length == 0)
        {
            return result;
        }

        Span<double> typicalPrice = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];

        for (var i = 0; i < length; i++)
        {
            var candle = candles[i];

            typicalPrice[i] = (double)(candle.Mid_C + candle.Mid_H + candle.Mid_L) / 3;
        }

        var rolStdDev = typicalPrice.CalcRolStdDev(window);

        var sma = typicalPrice.CalcSma(window);

        for (var i = 0; i < length; i++)
        {
            if (i < window - 1)
            {
                result[i] = new BollingerBandsResult
                {
                    Candle = candles[i],
                    Sma = sma[i],
                    UpperBand = 0.0,
                    LowerBand = 0.0
                };
                
                continue;
            }
            
            var band = rolStdDev[i] * stdDev;

            result[i] = new BollingerBandsResult
            {
                Candle = candles[i],
                Sma = sma[i],
                UpperBand = sma[i] + band,
                LowerBand = sma[i] - band
            };
        }

        return result;
    }
}