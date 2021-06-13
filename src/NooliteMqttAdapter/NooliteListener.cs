using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NooliteMqttAdapter.Devices;
using NooliteMqttAdapter.NooliteAdapter;
using Serilog;
using ThinkingHome.NooLite;
using ThinkingHome.NooLite.Internal;

namespace NooliteMqttAdapter
{
    internal sealed class NooliteListener : IDisposable
    {
        private readonly IMtrfAdapter _adapter;
        private readonly DevicesRepository _devicesRepository;
        private readonly MqttCommandPublisher _commandPublisher;
        private readonly ILogger _log;
        private readonly Lazy<IReadOnlyDictionary<int, Sensor>> _rxChannelToSensorConfig;
        private readonly Lazy<IReadOnlyDictionary<int, Switch>> _rxChannelToSwitchConfig;
        private readonly Lazy<IReadOnlyDictionary<int, Switch>> _txChannelToSwitchConfig;

        public NooliteListener(
            IMtrfAdapter adapter,
            DevicesRepository devicesRepository,
            MqttCommandPublisher commandPublisher,
            ILogger log)
        {
            _adapter = adapter;
            _devicesRepository = devicesRepository;
            _commandPublisher = commandPublisher;
            _log = log;
            _rxChannelToSensorConfig = new Lazy<IReadOnlyDictionary<int, Sensor>>(LoadSensorConfig);
            _rxChannelToSwitchConfig = new Lazy<IReadOnlyDictionary<int, Switch>>(LoadRxSwitchConfig);
            _txChannelToSwitchConfig = new Lazy<IReadOnlyDictionary<int, Switch>>(LoadTxSwitchConfig);
            
            _microclimateHandler = async (_, data) => await AdapterOnReceiveData(data);
        }

        private readonly EventHandler<ReceivedData> _microclimateHandler; 
        public void Activate()
        {
            _adapter.ReceiveData += _microclimateHandler;
            _adapter.Activate();
            _log.Debug("Noolite sensor started.");
        }

        private IReadOnlyDictionary<int, Sensor> LoadSensorConfig()
        {
            var dictionary = new Dictionary<int, Sensor>();
            foreach (var sensor in _devicesRepository.GetAllSensors())
            {
                try
                {
                    dictionary.Add(sensor.Channel, sensor);
                }
                catch
                {
                    _log.Error("Found duplicated sensor channels in the configuration file, channel: {channel}", sensor.Channel);
                }
            }

            return dictionary;
        }
        
        private IReadOnlyDictionary<int, Switch> LoadTxSwitchConfig()
        {
            var dictionary = new Dictionary<int, Switch>();
            foreach (var @switch in _devicesRepository.GetAllSwitches())
            {
                try
                {
                    dictionary.Add(@switch.Channel, @switch);
                }
                catch
                {
                    _log.Error("Found duplicated switch channels in the configuration file, channel: {channel}", @switch.Channel);
                }
            }

            return dictionary;
        }
        
        private IReadOnlyDictionary<int, Switch> LoadRxSwitchConfig()
        {
            var dictionary = new Dictionary<int, Switch>();
            foreach (var @switch in _devicesRepository.GetAllSwitches())
            foreach (var channel in @switch.StatusReportChannels)
            {
                try
                {
                    dictionary.Add(channel, @switch);
                }
                catch
                {
                    _log.Error("Found duplicated switch status report channels in the configuration file, channel: {channel}", channel);
                }
            }

            return dictionary;
        }
        
        private async Task AdapterOnReceiveData(ReceivedData receivedData)
        {
            Guard.DebugAssertArgumentNotNull(receivedData, nameof(receivedData));

            Switch? switchInfo;
            
            switch (receivedData.Mode, receivedData.Command, receivedData.Result)
            {
                case (MTRFXXMode.Service, _, _):
                    // this happens at startup when block exits service mode
                    break;
                
                case (MTRFXXMode.RX, MTRFXXCommand.BrightnessStop, ResultCode.Success):
                    // won't support continuous brightness change now
                    break;

                case (MTRFXXMode.RX, MTRFXXCommand.On, ResultCode.Success):
                case (MTRFXXMode.RXF, MTRFXXCommand.On, ResultCode.Success):
                case (MTRFXXMode.RX, MTRFXXCommand.TemporarySwitchOn, ResultCode.Success):
                case (MTRFXXMode.RXF, MTRFXXCommand.TemporarySwitchOn, ResultCode.Success):
                case (MTRFXXMode.RX, MTRFXXCommand.BrightnessUp, ResultCode.Success):
                case (MTRFXXMode.RXF, MTRFXXCommand.BrightnessUp, ResultCode.Success):
                    switchInfo = TryGetSwitchInfo(receivedData);
                    if (switchInfo?.StatusReportMqttTopic != null)
                    {
                        await _commandPublisher.PublishTurnOnAsync(switchInfo.StatusReportMqttTopic);
                        _log.Information("Switch turned on: {topic}", switchInfo.StatusReportMqttTopic);
                    }

                    var onSensorInfo = TryGetSensorInfo<OnOffSensor>(receivedData);
                    if (onSensorInfo?.MqttTopic != null)
                    {
                        await _commandPublisher.PublishTurnOnAsync(onSensorInfo.MqttTopic);
                        _log.Information("Sensor activated: {topic}", onSensorInfo.MqttTopic);
                    }
                    else if (switchInfo == null)
                    {
                        _log.Error("Can't find switch with StatusReportChannel {channel} or StatusReportMqttTopic is empty, or can't find sensor with the same Channel, or sensor's MqttTopic is empty", receivedData.Channel);
                    }

                    break;
                
                case (MTRFXXMode.RX, MTRFXXCommand.Off, ResultCode.Success):
                case (MTRFXXMode.RXF, MTRFXXCommand.Off, ResultCode.Success):
                case (MTRFXXMode.RX, MTRFXXCommand.BrightnessDown, ResultCode.Success):
                case (MTRFXXMode.RXF, MTRFXXCommand.BrightnessDown, ResultCode.Success):
                    switchInfo = TryGetSwitchInfo(receivedData);
                    if (switchInfo?.StatusReportMqttTopic != null)
                    {
                        await _commandPublisher.PublishTurnOffAsync(switchInfo.StatusReportMqttTopic);
                        _log.Information("Switch turned off: {topic}", switchInfo.StatusReportMqttTopic);
                    }

                    var offSensorInfo = TryGetSensorInfo<OnOffSensor>(receivedData);
                    if (offSensorInfo?.MqttTopic != null)
                    {
                        await _commandPublisher.PublishTurnOffAsync(offSensorInfo.MqttTopic);
                        _log.Information("Sensor deactivated: {topic}", offSensorInfo.MqttTopic);
                    }
                    else if (switchInfo == null)
                    {
                        _log.Error("Can't find switch with StatusReportChannel {channel} or StatusReportMqttTopic is empty, or can't find sensor with the same Channel, or sensor's MqttTopic is empty", receivedData.Channel);
                    }

                    break;
                    
                case (MTRFXXMode.TXF, MTRFXXCommand.On, ResultCode.NoResponse):
                case (MTRFXXMode.TXF, MTRFXXCommand.Off, ResultCode.NoResponse):
                    _log.Warning("No response from TX channel {channel}", receivedData.Channel);
                    //todo noResponse event?
                    break;
                
                case (MTRFXXMode.TXF, MTRFXXCommand.SendState, ResultCode.Success) when receivedData.Data3 == 1:
                case (MTRFXXMode.TX, MTRFXXCommand.On, ResultCode.Success):
                    switchInfo = TryGetSwitchInfo(receivedData);
                    if (switchInfo?.StatusReportMqttTopic != null)
                    {
                        await _commandPublisher.PublishTurnOnAsync(switchInfo.StatusReportMqttTopic);
                        _log.Information("Switch turned on: {topic}", switchInfo.StatusReportMqttTopic);
                    }
                    else if (switchInfo == null)
                    {
                        _log.Error("Can't find switch with Channel: {channel}, or StatusReportMqttTopic is empty", receivedData.Channel);
                    }
                    break;
                
                case (MTRFXXMode.TXF, MTRFXXCommand.SendState, ResultCode.Success) when receivedData.Data3 == 0:
                case (MTRFXXMode.TX, MTRFXXCommand.Off, ResultCode.Success):
                    switchInfo = TryGetSwitchInfo(receivedData);
                    if (switchInfo?.StatusReportMqttTopic != null)
                    {
                        await _commandPublisher.PublishTurnOffAsync(switchInfo.StatusReportMqttTopic);
                        _log.Information("Switch turned off: {topic}", switchInfo.StatusReportMqttTopic);
                    }
                    else if (switchInfo == null)
                    {
                        _log.Error("Can't find switch with Channel: {channel}, or StatusReportMqttTopic is empty", receivedData.Channel);
                    }
                    break;
                
                case (MTRFXXMode.RX, MTRFXXCommand.MicroclimateData, ResultCode.Success) when receivedData is MicroclimateData microclimateData:
                    var temperatureSensor = TryGetSensorInfo<TemperatureSensor>(receivedData);
                    if (temperatureSensor?.MqttTopic != null)
                    {
                        await _commandPublisher.PublishAsync(temperatureSensor.MqttTopic, microclimateData.Temperature.ToString("F1"));
                        _log.Information("Temperature of {topic} is {temperature}", temperatureSensor.MqttTopic, microclimateData.Temperature);

                        // TEMPORARY, waiting for Alice scenarios to support sensors
                        if (temperatureSensor is not TemperatureAndHumiditySensor)
                        {
                            if (microclimateData.Temperature < 20)
                            {
                                await _commandPublisher.PublishTurnOnAsync("balcony/heating");
                                _log.Verbose("Turned on balcony heating");
                            }
                            else
                            {
                                await _commandPublisher.PublishTurnOffAsync("balcony/heating");
                                _log.Verbose("Turned off balcony heating");
                            }
                        }
                    }
                    else
                    {
                        _log.Error("Can't find temperature sensor with Channel: {channel}, or MqttTopic is empty", receivedData.Channel);
                    }
                    
                    if (microclimateData.Humidity.HasValue &&
                        temperatureSensor is TemperatureAndHumiditySensor humiditySensor &&
                        humiditySensor.HumidityMqttTopic != null)
                    {
                        await _commandPublisher.PublishAsync(humiditySensor.HumidityMqttTopic, microclimateData.Humidity.Value.ToString("D"));
                        _log.Information("Humidity of {topic} is {humidity}", humiditySensor.HumidityMqttTopic, microclimateData.Humidity.Value);
                        
                        // TEMPORARY, waiting for Alice scenarios to support sensors
                        if (microclimateData.Humidity.Value > 50)
                        {
                            await _commandPublisher.PublishTurnOnAsync("bathroom/ventilation");
                            _log.Verbose("Turned on bathroom vent");
                        }
                        else
                        {
                            await _commandPublisher.PublishTurnOffAsync("bathroom/ventilation");
                            _log.Verbose("Turned off bathroom vent");
                        }
                    }
                    else if (microclimateData.Humidity.HasValue)
                    {
                        _log.Error("Can't find humidity sensor with Channel: {channel}, or HumidityMqttTopic is empty", receivedData.Channel);
                    }

                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(receivedData));
            }
        }

        private Switch? TryGetSwitchInfo(ReceivedData receivedData)
        {
            Switch? info;
            switch (receivedData.Mode)
            {
                case MTRFXXMode.TX:
                case MTRFXXMode.TXF:
                    if (!_txChannelToSwitchConfig.Value.TryGetValue(receivedData.Channel, out info))
                        return null;
                    return info;

                case MTRFXXMode.RX:
                case MTRFXXMode.RXF:
                    if (!_rxChannelToSwitchConfig.Value.TryGetValue(receivedData.Channel, out info))
                        return null;
                    return info;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(receivedData));
            }
        }
        
        private TInfo? TryGetSensorInfo<TInfo>(ReceivedData receivedData) where TInfo : Sensor
        {
            if (!_rxChannelToSensorConfig.Value.TryGetValue(receivedData.Channel, out var info))
                return null;

            if (info is TInfo typedInfo)
            {
                return typedInfo;
            }
            
            _log.Error("Sensor with channel {channel} has invalid type {actualType}. Expected type: {expectedType}",
                receivedData.Channel, info.GetType(), typeof(TInfo));
            return null;
        }

        public void Dispose()
        {
            _adapter.ReceiveData -= _microclimateHandler;
        }
    }
}
