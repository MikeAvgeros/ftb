namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static RsiResult[] CalcRsi(this Candle[] candles, int window = 14)
    {
        var length = candles.Length;

        var gains = new double[length];

        var losses = new double[length];

        var lastValue = 0.0;

        for (var i = 0; i < length; i++)
        {
            var value = (double)candles[i].Mid_C;

            if (i == 0)
            {
                gains[i] = 0.0;

                losses[i] = 0.0;

                lastValue = value;

                continue;
            }

            gains[i] = value > lastValue ? value - lastValue : 0.0;

            losses[i] = value < lastValue ? lastValue - value : 0.0;

            lastValue = value;
        }

        var gainsRma = gains.CalcRma(window).ToArray();

        var lossesRma = losses.CalcRma(window).ToArray();

        var result = new RsiResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new RsiResult();

            result[i].Candle = candles[i];

            result[i].AverageGain = gainsRma[i];

            result[i].AverageLoss = lossesRma[i];

            if (i > 0)
            {
                var rs = result[i].AverageGain / result[i].AverageLoss;

                result[i].Rsi = 100.0 - 100.0 / (1.0 + rs);
            }
            else
            {
                result[i].Rsi = 0.0;
            }
        }

        return result;
    }
}