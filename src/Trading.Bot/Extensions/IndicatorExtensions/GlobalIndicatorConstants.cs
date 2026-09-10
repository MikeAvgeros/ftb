namespace Trading.Bot.Extensions.IndicatorExtensions;

public static partial class Indicator
{
    private const double EntryZ = 2.25;
    private const double ExitZ = 0.25;
    private const double StopZ = 3.0;
    private const int MaxStackAlloc = 512;
    
    private const double VolumeZThreshold = 1.0;
    private const double LowVolumeZThreshold = -0.5;
    private const double VolatilitySpikeRatio = 1.5;
    private const double MomentumZThreshold = 1.0;
    private const int MomentumShortWindowDivisor = 5;
}