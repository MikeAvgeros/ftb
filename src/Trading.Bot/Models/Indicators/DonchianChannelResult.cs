namespace Trading.Bot.Models.Indicators;

public class DonchianChannelResult : IndicatorResult
{
    public decimal UpperBand { get; set; }
    public decimal LowerBand { get; set; }
    public decimal MidBand { get; set; }
}