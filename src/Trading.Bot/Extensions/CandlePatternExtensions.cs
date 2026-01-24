namespace Trading.Bot.Extensions;

public static class CandlePatternExtensions
{
    private const decimal HangingManBody = 15;
    private const decimal HangingManHeight = 75;
    private const decimal ShootingStarHeight = 75;
    private const decimal SpinningTopMin = 40;
    private const decimal SpinningTopMax = 60;
    private const decimal Marubozu = 98;
    private const decimal EngulfingFactor = 1.2m;
    private const decimal TweezerBody = 15;
    private const decimal TweezerTopBody = 40;
    private const decimal TweezerBottomBody = 60;
    private const decimal TweezerHlPercentageDifference = 0.01m;
    private const decimal MorningStarPrev2Body = 90;
    private const decimal MorningStarPrevBody = 10;

    public static bool IsHangingMan(this Candle candle)
    {
        if (candle is null) return false;

        return candle.BodyBottomPercentage > HangingManHeight &&
               candle.BodyPercentage < HangingManBody;
    }

    public static bool IsShootingStar(this Candle candle)
    {
        if (candle is null) return false;

        return candle.BodyTopPercentage < ShootingStarHeight &&
               candle.BodyPercentage < HangingManBody;
    }

    public static bool IsSpinningTop(this Candle candle)
    {
        if (candle is null) return false;

        return candle.BodyTopPercentage < SpinningTopMax &&
               candle.BodyBottomPercentage > SpinningTopMin &&
               candle.BodyPercentage < HangingManBody;
    }

    public static bool IsMarubozu(this Candle candle) => candle.BodyPercentage > Marubozu;

    public static bool IsEngulfingCandle(this Candle candle, Candle prevCandle)
    {
        if (candle is null || prevCandle is null) return false;

        return candle.Direction != prevCandle.Direction &&
               candle.BodySize > prevCandle.BodySize * EngulfingFactor;
    }

    public static bool IsTweezerTop(this Candle candle, Candle prevCandle)
    {
        if (candle is null || prevCandle is null) return false;

        var lowChange = (candle.Mid_L - prevCandle.Mid_L) / prevCandle.Mid_L * 100;

        var highChange = (candle.Mid_H - prevCandle.Mid_H) / prevCandle.Mid_H * 100;

        var bodyChange = (candle.BodySize - prevCandle.BodySize) / prevCandle.BodySize * 100;

        return Math.Abs(bodyChange) < TweezerBody && candle.Direction == -1 &&
               candle.Direction != prevCandle.Direction &&
               Math.Abs(lowChange) < TweezerHlPercentageDifference &&
               Math.Abs(highChange) < TweezerHlPercentageDifference &&
               candle.BodyTopPercentage < TweezerTopBody;
    }

    public static bool IsTweezerBottom(this Candle candle, Candle prevCandle)
    {
        if (candle is null || prevCandle is null) return false;

        var lowChange = (candle.Mid_L - prevCandle.Mid_L) / prevCandle.Mid_L * 100;

        var highChange = (candle.Mid_H - prevCandle.Mid_H) / prevCandle.Mid_H * 100;

        var bodyChange = (candle.BodySize - prevCandle.BodySize) / prevCandle.BodySize * 100;

        return Math.Abs(bodyChange) < TweezerBody && candle.Direction == 1 &&
               candle.Direction != prevCandle.Direction &&
               Math.Abs(lowChange) < TweezerHlPercentageDifference &&
               Math.Abs(highChange) < TweezerHlPercentageDifference &&
               candle.BodyBottomPercentage > TweezerBottomBody;
    }

    public static bool IsMorningStar(this Candle candle, Candle[] lastTwoCandles, int direction = 1)
    {
        if (candle is null || lastTwoCandles is null || lastTwoCandles.Length != 2) return false;

        var prev2Candle = lastTwoCandles.OrderBy(c => c.Time).First();

        var prevCandle = lastTwoCandles.OrderBy(c => c.Time).Last();

        return prev2Candle.BodyPercentage > MorningStarPrev2Body &&
               prevCandle.BodyPercentage < MorningStarPrevBody &&
               candle.Direction == direction && prev2Candle.Direction != direction &&
               (direction == 1 && candle.Mid_C > prev2Candle.MidPoint ||
                direction == -1 && candle.Mid_C < prev2Candle.MidPoint);
    }

    public static bool HigherHighs(this Candle[] candles)
    {
        var higherHighs = 0;

        var latestHigh = candles[0].Mid_H;

        var length = candles.Length - 1;

        for (var i = 1; i < length; i++)
        {
            if (!IsSwingHigh(candles, i) || !(candles[i].Mid_H > latestHigh)) continue;

            latestHigh = candles[i].Mid_H;

            higherHighs++;
        }

        return higherHighs > 1;
    }

    public static bool LowerHighs(this Candle[] candles)
    {
        var lowerHighs = 0;

        var latestHigh = candles[0].Mid_H;

        var length = candles.Length - 1;

        for (var i = 1; i < length; i++)
        {
            if (!IsSwingLow(candles, i) || !(candles[i].Mid_H < latestHigh)) continue;

            latestHigh = candles[i].Mid_H;

            lowerHighs++;
        }

        return lowerHighs > 1;
    }

    public static bool HigherLows(this Candle[] candles)
    {
        var higherLows = 0;

        var latestLow = candles[0].Mid_L;

        var length = candles.Length - 1;

        for (var i = 1; i < length; i++)
        {
            if (!IsSwingHigh(candles, i) || !(candles[i].Mid_L > latestLow)) continue;

            latestLow = candles[i].Mid_L;

            higherLows++;
        }

        return higherLows > 1;
    }

    public static bool LowerLows(this Candle[] candles)
    {
        var lowerLows = 0;

        var latestLow = candles[0].Mid_L;

        var length = candles.Length - 1;

        for (var i = 1; i < length; i++)
        {
            if (!IsSwingLow(candles, i) || !(candles[i].Mid_L < latestLow)) continue;

            latestLow = candles[i].Mid_L;

            lowerLows++;
        }

        return lowerLows > 1;
    }

    public static double CalcResistance(this Candle[] candles)
    {
        var resistanceLevels = new List<double>();

        var prices = candles.Select(c => (double)c.Mid_H).ToArray();

        var length = prices.Length - 1;

        for (var i = 0; i < length; i++)
        {
            if (i == 0 || IsSwingHigh(candles, i)) resistanceLevels.Add(prices[i]);
        }

        return resistanceLevels.Max();
    }

    public static double CalcSupport(this Candle[] candles)
    {
        var supportLevels = new List<double>();

        var prices = candles.Select(c => (double)c.Mid_L).ToArray();

        var length = prices.Length - 1;

        for (var i = 0; i < length; i++)
        {
            if (i == 0 || IsSwingLow(candles, i)) supportLevels.Add(prices[i]);
        }

        return supportLevels.Min();
    }

    private static bool IsSwingHigh(Candle[] candles, int index)
    {
        if (index < 1 || index >= candles.Length - 1) return false;

        return candles[index].Mid_H > candles[index - 1].Mid_H && candles[index].Mid_H > candles[index + 1].Mid_H;
    }

    private static bool IsSwingLow(Candle[] candles, int index)
    {
        if (index < 1 || index >= candles.Length - 1) return false;

        return candles[index].Mid_L < candles[index - 1].Mid_L && candles[index].Mid_L < candles[index + 1].Mid_L;
    }
}