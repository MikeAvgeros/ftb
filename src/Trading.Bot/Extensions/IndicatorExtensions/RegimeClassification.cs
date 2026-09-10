namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private static SpreadRegime ClassifySpreadRegime(double zScore, double[] spreadHistory, double[] volumeHistory)
    {
        if (Math.Abs(zScore) < EntryZ) return SpreadRegime.None;
        
        if (spreadHistory.Length < 3) return SpreadRegime.Ambiguous;

        var window = spreadHistory.Length;

        var shortWindow = Math.Max(3, window / MomentumShortWindowDivisor);

        var longVolatility = spreadHistory.CalcRolStdDev(window)[^1];

        var shortVolatility = spreadHistory.CalcRolStdDev(shortWindow)[^1];

        var volatilityRatio = longVolatility == 0 ? 0 : shortVolatility / longVolatility;

        var trend = spreadHistory.CalcTrendLine();

        var momentumScore = longVolatility == 0 ? 0 : (trend[^1] - trend[0]) / longVolatility;

        var volumeZ = volumeHistory.CalcWinsorizedZScore();

        var hasMomentum = Math.Abs(momentumScore) > MomentumZThreshold &&
                           Math.Sign(momentumScore) == Math.Sign(zScore);

        var hasAbnormalVolume = volumeZ > VolumeZThreshold;

        var hasLowVolume = volumeZ < LowVolumeZThreshold;

        var hasVolatilitySpike = volatilityRatio > VolatilitySpikeRatio;

        if (hasMomentum) return SpreadRegime.MomentumContinuation;

        if (hasAbnormalVolume) return SpreadRegime.AbnormalVolumeContinuation;

        if (hasLowVolume) return SpreadRegime.LowVolumeReversion;

        if (hasVolatilitySpike) return SpreadRegime.VolatilitySpikeReversion;

        return SpreadRegime.Ambiguous;
    }
    
    private static Signal GetContinuationSignal(Signal reversionSignal) => (Signal)(-(int)reversionSignal);

    private static Signal ResolveRegimeSignal(SpreadRegime regime, Signal reversionSignal)
    {
        return regime switch
        {
            SpreadRegime.LowVolumeReversion or SpreadRegime.VolatilitySpikeReversion => reversionSignal,
            SpreadRegime.MomentumContinuation or SpreadRegime.AbnormalVolumeContinuation =>
                GetContinuationSignal(reversionSignal),
            _ => Signal.None
        };
    }
}
