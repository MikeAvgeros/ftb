namespace Trading.Bot.Services;

public class RolloverManager(ILogger<RolloverManager> logger, OandaApiService apiService)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new CronTimer("55 21 * * 5", TimeZoneInfo.Utc);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var openTrades = await apiService.GetOpenTrades();

                foreach (var trade in openTrades)
                {
                    await apiService.CloseTrade(trade.Id);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "An error occurred while trying to close all open trades");
            }
        }
    }
}