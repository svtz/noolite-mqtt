using System;
using System.Linq;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Client.Receiving;
using MQTTnet.Extensions.ManagedClient;
using Newtonsoft.Json;
using NooliteMqttAdapter.Devices;
using NooliteMqttAdapter.NooliteAdapter;
using Serilog;

namespace NooliteMqttAdapter
{
    internal sealed class IncomingListener
    {
        private readonly ILogger _logger;
        private readonly DevicesRepository _devicesRepository;
        private readonly IManagedMqttClient _mqttClient;
        private readonly IMtrfAdapter _mtrfAdapter;

        public IncomingListener(ILogger logger, DevicesRepository devicesRepository, IManagedMqttClient mqttClient, IMtrfAdapter mtrfAdapter)
        {
            _logger = logger;
            _devicesRepository = devicesRepository;
            _mqttClient = mqttClient;
            _mtrfAdapter = mtrfAdapter;
        }

        public async Task Start()
        {
            _mqttClient.UseApplicationMessageReceivedHandler(Handler);
            var topicFilters = _devicesRepository.GetAllSwitches()
                .Select(s => new MqttTopicFilterBuilder()
                    .WithTopic(s.MqttTopic)
                    .Build());
            await _mqttClient.SubscribeAsync(topicFilters);

            _logger.Debug("IncomingListener started.");
        }
        
        private Task Handler(MqttApplicationMessageReceivedEventArgs message)
        {
            _logger.Debug($"Incoming message: {JsonConvert.SerializeObject(message.ApplicationMessage, Formatting.Indented)}");
            return Task.CompletedTask;
        }
    }
}