namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static DonchianChannelResult[] CalcDonchianChannel(this Candle[] candles, int window = 20)
    {
        var length = candles.Length;

        var result = new DonchianChannelResult[length];
        
        if (length == 0)
        {
            return result;
        }
        
        Span<int> maxDeque = length <= MaxStackAlloc ? stackalloc int[length] : new int[length];

        Span<int> minDeque = length <= MaxStackAlloc ? stackalloc int[length] : new int[length];

        var maxHead = 0;

        var maxTail = 0;

        var minHead = 0;

        var minTail = 0;

        for (var i = 0; i < length; i++)
        {
            if (i > 0)
            {
                var prevIndex = i - 1;
                
                var prevHigh = (double)candles[prevIndex].Mid_H;
                
                var prevLow = (double)candles[prevIndex].Mid_L;
                
                while (maxTail > maxHead && (double)candles[maxDeque[maxTail - 1]].Mid_H <= prevHigh)
                {
                    maxTail--;
                }
                
                maxDeque[maxTail++] = prevIndex;
                
                while (minTail > minHead && (double)candles[minDeque[minTail - 1]].Mid_L >= prevLow)
                {
                    minTail--;
                }
                
                minDeque[minTail++] = prevIndex;
            }

            while (maxTail > maxHead && maxDeque[maxHead] < i - window)
            {
                maxHead++;
            }

            while (minTail > minHead && minDeque[minHead] < i - window)
            {
                minHead++;
            }

            if (i >= window)
            {
                var upperBand = candles[maxDeque[maxHead]].Mid_H;

                var lowerBand = candles[minDeque[minHead]].Mid_L;

                result[i] = new DonchianChannelResult
                {
                    Candle = candles[i],
                    UpperBand = upperBand,
                    LowerBand = lowerBand,
                    MidBand = (upperBand + lowerBand) / 2
                };
            }
            else
            {
                result[i] = new DonchianChannelResult
                {
                    Candle = candles[i],
                    UpperBand = 0,
                    LowerBand = 0,
                    MidBand = 0
                };
            }
        }

        return result;
    }
}