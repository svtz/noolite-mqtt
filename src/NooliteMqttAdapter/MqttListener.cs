﻿using System;
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
    internal sealed class MqttListener
    {
        private readonly ILogger _logger;
        private readonly DevicesRepository _devicesRepository;
        private readonly IManagedMqttClient _mqttClient;
        private readonly IMtrfAdapter _mtrfAdapter;
        
        public MqttListener(ILogger logger, DevicesRepository devicesRepository, IManagedMqttClient mqttClient, IMtrfAdapter mtrfAdapter)
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

        private async Task Handler(MqttApplicationMessageReceivedEventArgs message)
        {
            var command = message.ApplicationMessage.Payload != null
                ? MqttCommands.Encoding.GetString(message.ApplicationMessage.Payload)
                : "null";
            var topic = message.ApplicationMessage.Topic; 
            _logger.Debug("Incoming message topic: {topic}, content: {command}", 
                topic,
                command);

            Func<Switch, Task>? action = command switch
            {
                MqttCommands.TurnOff => async s =>
                {
                    _mtrfAdapter.OffF(s.Channel);
                    if (s.StatusReportMqttTopic != null)
                        await _mqttClient.PublishAsync(MqttCommands.CreateTurnOff(s.StatusReportMqttTopic));
                    else
                        _logger.Error("StatusReportMqttTopic is not specified for switch with Channel: {channel}", s.Channel);
                },
                MqttCommands.TurnOn => async s =>
                {
                    _mtrfAdapter.OnF(s.Channel);
                    if (s.StatusReportMqttTopic != null)
                        await _mqttClient.PublishAsync(MqttCommands.CreateTurnOn(s.StatusReportMqttTopic));
                    else
                        _logger.Error("StatusReportMqttTopic is not specified for switch with Channel: {channel}", s.Channel);
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
    }
}