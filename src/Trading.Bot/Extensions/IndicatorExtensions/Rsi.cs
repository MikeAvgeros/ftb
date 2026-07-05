namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static RsiResult[] CalcRsi(this Candle[] candles, int window = 14)
    {
        var length = candles.Length;
        
        var result = new RsiResult[length];

        if (length == 0)
        {
            return result;
        }
        
        Span<double> gains = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];
        
        Span<double> losses = length <= MaxStackAlloc ? stackalloc double[length] : new double[length];

        var lastValue = (double)candles[0].Mid_C;

        for (var i = 1; i < length; i++)
        {
            var value = (double)candles[i].Mid_C;
            
            gains[i] = value > lastValue ? value - lastValue : 0.0;
            
            losses[i] = value < lastValue ? lastValue - value : 0.0;
            
            lastValue = value;
        }
        
        var alpha = 1.0 / window;
        
        var sumGain = 0.0;
        
        var sumLoss = 0.0;

        for (var i = 0; i < length; i++)
        {
            if (i < window)
            {
                sumGain += gains[i];
                
                sumLoss += losses[i];

                var rsiValue = double.NaN;
                
                if (i == window - 1)
                {
                    sumGain /= window;
                    
                    sumLoss /= window;
                    
                    if (sumLoss == 0.0)
                    {
                        rsiValue = sumGain == 0.0 ? 50.0 : 100.0;
                    }
                    else
                    {
                        var rs = sumGain / sumLoss;
                        
                        rsiValue = 100.0 - (100.0 / (1.0 + rs));
                    }
                }

                result[i] = new RsiResult
                {
                    Candle = candles[i],
                    AverageGain = i == window - 1 ? sumGain : 0.0,
                    AverageLoss = i == window - 1 ? sumLoss : 0.0,
                    Rsi = rsiValue
                };
                
                continue;
            }
            
            sumGain = alpha * gains[i] + (1.0 - alpha) * sumGain;
            
            sumLoss = alpha * losses[i] + (1.0 - alpha) * sumLoss;

            double rsi;
            
            if (sumLoss == 0.0)
            {
                rsi = sumGain == 0.0 ? 50.0 : 100.0; 
            }
            else
            {
                var rs = sumGain / sumLoss;
                rsi = 100.0 - (100.0 / (1.0 + rs));
            }

            result[i] = new RsiResult
            {
                Candle = candles[i],
                AverageGain = sumGain,
                AverageLoss = sumLoss,
                Rsi = rsi
            };
        }

        return result;
    }
}