namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    public static DonchianChannelResult[] CalcDonchianChannel(this Candle[] candles, int window = 20)
    {
        var highs = candles.Select(c => c.Mid_H).ToArray();

        var lows = candles.Select(c => c.Mid_L).ToArray();

        var length = candles.Length;

        var result = new DonchianChannelResult[length];

        for (var i = 0; i < length; i++)
        {
            result[i] ??= new DonchianChannelResult();

            result[i].Candle = candles[i];

            result[i].UpperBand = i >= window ? highs.Take(i).TakeLast(window).Max() : 0;

            result[i].LowerBand = i >= window ? lows.Take(i).TakeLast(window).Min() : 0;

            result[i].MidBand = i >= window ? (result[i].UpperBand + result[i].LowerBand) / 2 : 0;
        }

        return result;
    }
}