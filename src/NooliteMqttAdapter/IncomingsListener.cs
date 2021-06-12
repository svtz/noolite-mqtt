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
            var command = message.ApplicationMessage.Payload != null
                ? System.Text.Encoding.UTF8.GetString(message.ApplicationMessage.Payload)
                : "null";
            var topic = message.ApplicationMessage.Topic; 
            _logger.Debug("Incoming message topic: {topic}, content: {command}", 
                topic,
                command);

            Action<Switch> action;
            switch (command)
            {
                case "0":
                    action = s =>
                    {
                        _mtrfAdapter.SetBrightnessF(s.Channel, s.ZeroPowerValue);
                        _mtrfAdapter.OffF(s.Channel);
                    };
                    break;
                case "1":
                    action = s =>
                    {
                        _mtrfAdapter.SetBrightnessF(s.Channel, s.FullPowerValue);
                        _mtrfAdapter.OnF(s.Channel);
                    };
                    break;
                default:
                    _logger.Error("Received unknown command from {topic}, the command was: {command}", topic, command);
                    return Task.CompletedTask;
            }

            var switches = _devicesRepository.GetAllSwitches()
                .Where(s => string.Equals(s.MqttTopic, topic, StringComparison.Ordinal))
                .ToArray();
            foreach (var @switch in switches)
            {
                action(@switch);
            }
            
            return Task.CompletedTask;
        }
    }
}