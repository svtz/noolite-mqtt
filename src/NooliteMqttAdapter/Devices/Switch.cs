using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Serilog;

namespace NooliteMqttAdapter.Devices
{
    internal abstract class Device
    {
        public byte Channel { get; set; }
        public string? MqttTopic { get; set; }
        public string Description { get; set; } = string.Empty;
    }
    
    internal class Switch : Device
    {
        public byte[] StatusReportChannels { get; set; } = Array.Empty<byte>();
        public byte FullPowerValue { get; set; }
        public byte ZeroPowerValue { get; set; }
        
        public string? StatusReportMqttTopic { get; set; }
    }

    internal abstract class Sensor : Device
    {
    }
    
    internal class OnOffSensor : Sensor
    {
    }
    
    internal class TemperatureSensor : Sensor
    {
    }
    
    internal class TemperatureAndHumiditySensor : TemperatureSensor
    {
        public string? HumidityMqttTopic { get; set; }
    }

    internal class DevicesRepository
    {
        private readonly ILogger _logger;
        private readonly Device[] _devices;
        
        public DevicesRepository(ILogger logger)
        {
            _logger = logger;
            try
            {
                var configFileContent = File.ReadAllText("devices.json");
                _devices = JsonConvert.DeserializeObject<Device[]>(configFileContent, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.Auto
                })!;
            }
            catch
            {
                logger.Error("Error reading or deserializing devices.json.");
                throw;
            }
        }

        public IEnumerable<Switch> GetAllSwitches() => _devices.OfType<Switch>();
        public IEnumerable<Sensor> GetAllSensors() => _devices.OfType<Sensor>();
    }
}
