namespace Trading.Bot.Configuration;

public class TradeConfiguration
{
    public bool StopRollover { get; set; }
    public bool SendEmail { get; set; }
    public bool NotifyOnly { get; set; }
    public bool PairsTrading { get; set; }
    public int TradeRisk { get; set; }
    public TradeSettings[] TradeSettings { get; set; }
}