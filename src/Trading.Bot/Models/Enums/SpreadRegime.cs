namespace Trading.Bot.Models.Enums;

public enum SpreadRegime
{
    None = 0,
    LowVolumeReversion = 1,
    VolatilitySpikeReversion = 2,
    MomentumContinuation = 3,
    AbnormalVolumeContinuation = 4,
    Ambiguous = 5
}
