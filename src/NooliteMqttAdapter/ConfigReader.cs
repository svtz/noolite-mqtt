using System;
using Microsoft.Extensions.Configuration;
using NooliteMqttAdapter;
using Serilog.Events;

namespace NooliteMqttAdapter
{
    public class ConfigReader
    {
        private readonly IConfigurationRoot _configurationRoot;

        #region config
        
        public LogEventLevel LogLevel => 
            (LogEventLevel)Enum.Parse(typeof(LogEventLevel), _configurationRoot["LogEventLevel"]);

        public string MqttHost => _configurationRoot["MqttHost"];
        public string MqttUsername => _configurationRoot["MqttUsername"];
        public string MqttPassword => _configurationRoot["MqttPassword"];

        public string MtrfAdapterPort => _configurationRoot["MtrfAdapterPort"];
        
        #endregion
        
        
        private ConfigReader(IConfigurationRoot configurationRoot)
        {
            _configurationRoot = configurationRoot;
        }
        
        public static ConfigReader ReadConfig()
        {
            var builder = new ConfigurationBuilder()
                .AddJsonFile("settings.json")
                .AddEnvironmentVariables();

#if DEBUG
            builder.AddJsonFile("settings.Debug.json");
#else
            builder.AddJsonFile($"settings.Production.json");
#endif
            
            return new ConfigReader(builder.Build());
        }
    }
}
