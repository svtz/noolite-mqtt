using System;

namespace NooliteMqttAdapter.Devices
{
    internal class Switch : Device
    {
        public byte[] StatusReportChannels { get; set; } = Array.Empty<byte>();
        public byte FullPowerValue { get; set; }
        public byte ZeroPowerValue { get; set; }
        
        public string? StatusReportMqttTopic { get; set; }
    }
}
