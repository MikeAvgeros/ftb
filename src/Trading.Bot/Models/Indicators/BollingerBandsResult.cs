namespace Trading.Bot.Models.Indicators;

public class BollingerBandsResult : IndicatorResult
{
    public double Sma { get; set; }
    public double UpperBand { get; set; }
    public double LowerBand { get; set; }
}