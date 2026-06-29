
var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        var constants = context.Configuration
            .GetSection(nameof(Constants))
            .Get<Constants>();

        services.AddSingleton(constants);

        var tradeConfiguration = context.Configuration
            .GetSection(nameof(TradeConfiguration))
            .Get<TradeConfiguration>();

        services.AddSingleton(tradeConfiguration);

        var emailConfig = context.Configuration
            .GetSection("EmailConfiguration")
            .Get<EmailConfiguration>();

        services.AddSingleton(emailConfig);

        services.AddTransient<EmailService>();

        services.AddOandaApiService(constants);

        services.AddOandaStreamService(constants);

        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(context.Configuration)
            .CreateLogger();

        services.AddSingleton<LiveTradeCache>();

        if (tradeConfiguration.PairsTrading)
        {
            services.AddHostedService<PairsTradeManager>();
        }
        else
        {
            services.AddHostedService<TradeManager>();
        }

        services.AddHostedService<StreamProcessor>();

        services.AddHostedService<StreamWorker>();

        if (tradeConfiguration.StopRollover)
        {
            services.AddHostedService<RolloverManager>();
        }
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();

        logging.Services.AddSerilog();
    })
    .Build();

await host.RunAsync();