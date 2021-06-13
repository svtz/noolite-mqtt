namespace NooliteMqttAdapter.Devices
{
    internal class TemperatureAndHumiditySensor : TemperatureSensor
    {
        public string? HumidityMqttTopic { get; set; }
    }
}