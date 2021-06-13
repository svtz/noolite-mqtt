using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Serilog;

namespace NooliteMqttAdapter.Devices
{
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