namespace Trading.Bot.Extensions;

public static class MiscellaneousExtensions
{
    public static DateTime RoundDown(this DateTime time, TimeSpan candleSpan)
    {
        if (candleSpan.Days != 0)
        {
            return new DateTime(time.Year, time.Month, time.Day - time.Day % candleSpan.Days,
                0, 0, 0);
        }

        if (candleSpan.Hours != 0)
        {
            return new DateTime(time.Year, time.Month, time.Day,
                time.Hour - time.Hour % candleSpan.Hours, 0, 0);
        }

        if (candleSpan.Minutes != 0)
        {
            return new DateTime(time.Year, time.Month, time.Day, time.Hour,
                time.Minute - time.Minute % candleSpan.Minutes, 0);
        }

        return time;
    }

    public static bool IsSuccessStatusCode(this HttpStatusCode statusCode)
    {
        return (int)statusCode >= 200 && (int)statusCode <= 299;
    }

    public static decimal CalcTakeProfit(this Candle candle, IndicatorResult result, decimal riskReward)
    {
        return result.Signal switch
        {
            Signal.Buy => candle.Mid_C + result.Gain * riskReward,
            Signal.Sell => candle.Mid_C - result.Gain * riskReward,
            _ => 0
        };
    }

    public static decimal CalcStopLoss(this Candle candle, IndicatorResult result)
    {
        return result.Signal switch
        {
            Signal.Buy => candle.Mid_C - result.Gain,
            Signal.Sell => candle.Mid_C + result.Gain,
            _ => 0
        };
    }
}