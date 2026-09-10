namespace Trading.Bot.Models.Indicators;

public class SimulationSummary
{
    public string Strategy { get; set; }
    public int Days { get; set; }
    public int Candles { get; set; }
    public int Trades { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    public int Unknown { get; set; }
    public double WinRate { get; set; }
    public double BuyWinRate { get; set; }
    public double SellWinRate { get; set; }
    public int TradeRisk { get; set; }
    public double Balance { get; set; }
    public decimal TotalCosts { get; set; }
    public decimal NetPl { get; set; }
}