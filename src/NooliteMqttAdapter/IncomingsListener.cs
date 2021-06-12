using System;
using System.Linq;
using System.Text;
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
        private readonly Encoding _encoding = System.Text.Encoding.UTF8;
        
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

        private class MqttCommands
        {
            public const string TurnOff = "0";
            public const string TurnOn = "1";
        }
        
        private async Task Handler(MqttApplicationMessageReceivedEventArgs message)
        {
            var command = message.ApplicationMessage.Payload != null
                ? _encoding.GetString(message.ApplicationMessage.Payload)
                : "null";
            var topic = message.ApplicationMessage.Topic; 
            _logger.Debug("Incoming message topic: {topic}, content: {command}", 
                topic,
                command);

            Func<Switch, Task>? action = command switch
            {
                MqttCommands.TurnOff => async s =>
                {
                    _mtrfAdapter.SetBrightnessF(s.Channel, s.ZeroPowerValue);
                    _mtrfAdapter.OffF(s.Channel);
                    await SendMessage(s.StatusReportMqttTopic!, MqttCommands.TurnOff);
                },
                MqttCommands.TurnOn => async s =>
                {
                    _mtrfAdapter.SetBrightnessF(s.Channel, s.FullPowerValue);
                    _mtrfAdapter.OnF(s.Channel);
                    await SendMessage(s.StatusReportMqttTopic!, MqttCommands.TurnOn);
                },
                _ => null
            };

            if (action == null)
            {
                _logger.Error("Received unknown command from {topic}, the command was: {command}", topic, command);
                return;
            }

            var switches = _devicesRepository.GetAllSwitches()
                .Where(s => string.Equals(s.MqttTopic, topic, StringComparison.Ordinal))
                .ToArray();
            foreach (var @switch in switches)
            {
                await action(@switch);
            }
        }

        private async Task SendMessage(string topic, string content)
        {
            var message = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithRetainFlag()
                .WithPayload(_encoding.GetBytes(content))
                .WithAtLeastOnceQoS()
                .Build();

            await _mqttClient.PublishAsync(new ManagedMqttApplicationMessageBuilder()
                .WithApplicationMessage(message)
                .Build());
        }
    }
}