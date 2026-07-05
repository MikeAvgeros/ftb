namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static AtrResult[] CalcAtr(this Candle[] candles, int window = 14)
    {
        var length = candles.Length;
        
        var result = new AtrResult[length];
        
        if (length == 0)
        {
            return result;
        }
        
        Span<double> maxTr = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];
        
        for (var i = 0; i < length; i++)
        {
            var candle = candles[i];
            
            var prevMidC = i == 0 ? candle.Mid_C : candles[i - 1].Mid_C;

            var tr1 = (double)(candle.Mid_H - candle.Mid_L);
            
            var tr2 = (double)Math.Abs(candle.Mid_H - prevMidC);
            
            var tr3 = (double)Math.Abs(prevMidC - candle.Mid_L);

            maxTr[i] = Math.Max(tr1, Math.Max(tr2, tr3));
        }
        
        var alpha = 1.0 / window;
        
        var runningAtr = 0.0;

        for (var i = 0; i < length; i++)
        {
            if (i < window - 1)
            {
                result[i] = new AtrResult
                {
                    Candle = candles[i],
                    MaxTr = maxTr[i],
                    Atr = 0.0
                };
                
                continue;
            }
            
            if (i == window - 1)
            {
                double sumTr = 0;
                
                for (var j = 0; j <= i; j++)
                {
                    sumTr += maxTr[j];
                }
                
                runningAtr = sumTr / window;

                result[i] = new AtrResult
                {
                    Candle = candles[i],
                    MaxTr = maxTr[i],
                    Atr = runningAtr
                };
                
                continue;
            }
            
            runningAtr = alpha * maxTr[i] + (1.0 - alpha) * runningAtr;

            result[i] = new AtrResult
            {
                Candle = candles[i],
                MaxTr = maxTr[i],
                Atr = runningAtr
            };
        }

        return result;
    }
}