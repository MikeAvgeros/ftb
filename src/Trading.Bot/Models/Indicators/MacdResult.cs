namespace Trading.Bot.Models.Indicators;

public class MacdResult : IndicatorResult
{
    public double Macd { get; set; }
    public double SignalLine { get; set; }
    public double Histogram { get; set; }
}
