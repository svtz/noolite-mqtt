using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using MQTTnet;
using MQTTnet.Client.Options;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using NooliteMqttAdapter.Devices;
using NooliteMqttAdapter.NooliteAdapter;
using Serilog;

namespace NooliteMqttAdapter
{
    internal sealed class AdapterService
    {
        private ConfigReader Config { get; }
        private ILogger Logger { get; }

        private const string ServiceNamePrefix = "noolite";
        
        public AdapterService()
        {
            Config = ConfigReader.ReadConfig();
            Logger = new LoggerBuilder(Config).BuildLogger();
        }

        private static async Task Run(IServiceProvider serviceProvider, CancellationToken ct)
        {
            var listener = serviceProvider.GetRequiredService<MqttListener>();
            await listener.Start();
            await Task.Delay(-1, ct);
        }

        private void ConfigureServices(ServiceCollection services, string uniqueServiceName)
        {
            var options = new MqttClientOptionsBuilder()
                .WithClientId(uniqueServiceName)
                .WithProtocolVersion(MqttProtocolVersion.V311)
                .WithTcpServer(Config.MqttHost, Config.MqttPort)
                .WithCredentials(Config.MqttUsername, Config.MqttPassword)
                .Build();

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(options)
                .Build();

            services.AddScoped<IManagedMqttClient>(sp =>
                {
                    var client = new MqttFactory().CreateManagedMqttClient();
                    client.StartAsync(managedOptions).Wait();
                    return client;
                }
            );
            
            #if DEBUG
            services.AddSingleton<IMtrfAdapter, MtrfAdapterMock>();
            #else
            services.AddSingleton<IMtrfAdapter>(
                sp => new AdapterWrapper(Config.MtrfAdapterPort, sp.GetRequiredService<ILogger>()));
            #endif
            services.AddScoped<DevicesRepository>();
            services.AddScoped<MqttListener>();
        }
        
        private IServiceProvider CreateRootServiceProvider()
        {
            var uniqueServiceName = $"{ServiceNamePrefix}";

            var services = new ServiceCollection();
            services.AddSingleton(sp => Logger);
            services.AddSingleton(new CancellationTokenSource());
            ConfigureServices(services, uniqueServiceName);

            Logger.Debug($"Root service provider created");
            
            return services.BuildServiceProvider();
        }


        public async Task Run()
        {
            var version = GetType().Assembly.GetName().Version;
            var title = $"{ServiceNamePrefix} v.{version?.ToString(3) ?? "null"}";
            Console.Title = title;

            Logger.Information($"Starting service: {title}");

            using var serviceScope = CreateRootServiceProvider().CreateScope();
            var cts = serviceScope.ServiceProvider.GetRequiredService<CancellationTokenSource>();
            Console.CancelKeyPress += (s, e) => cts.Cancel();
            AppDomain.CurrentDomain.UnhandledException += (_, _) =>
            {
                if (!cts.IsCancellationRequested)
                    cts.Cancel();
            };

            await Run(serviceScope.ServiceProvider, cts.Token);
        }
    }
}