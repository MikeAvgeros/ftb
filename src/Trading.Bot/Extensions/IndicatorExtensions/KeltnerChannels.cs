namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static KeltnerChannelsResult[] CalcKeltnerChannels(this Candle[] candles, int emaWindow = 20, int atrWindow = 10, double multiplier = 2)
    {
        var length = candles.Length;

        var result = new KeltnerChannelsResult[length];

        var prices = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];

        for (var i = 0; i < length; i++)
        {
            prices[i] = (double)candles[i].Mid_C;
        }

        var ema = prices.CalcEma(emaWindow);

        var atr = candles.CalcAtr(atrWindow);

        for (var i = 0; i < length; i++)
        {
            if (i < atrWindow - 1)
            {
                result[i] = new KeltnerChannelsResult
                {
                    Candle = candles[i],
                    Ema = ema[i],
                    UpperBand = 0.0,
                    LowerBand = 0.0
                };

                continue;
            }

            var band = atr[i].Atr * multiplier;

            result[i] = new KeltnerChannelsResult
            {
                Candle = candles[i],
                Ema = ema[i],
                UpperBand = band + ema[i],
                LowerBand = ema[i] - band
            };
        }

        return result;
    }
}