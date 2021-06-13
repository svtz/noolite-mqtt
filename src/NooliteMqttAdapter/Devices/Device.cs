namespace NooliteMqttAdapter.Devices
{
    internal abstract class Device
    {
        public byte Channel { get; set; }
        public string? MqttTopic { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}