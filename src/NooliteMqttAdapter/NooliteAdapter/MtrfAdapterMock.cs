using System;
using Serilog;
using ThinkingHome.NooLite;

namespace NooliteMqttAdapter.NooliteAdapter
{
    internal class MtrfAdapterMock : IMtrfAdapter
    {
        private readonly ILogger _logger;

        public MtrfAdapterMock(ILogger logger)
        {
            _logger = logger.ForContext<MtrfAdapterMock>();
        }
        
        #pragma warning disable CS0067
        public event EventHandler<ReceivedData>? ReceiveData;
        #pragma warning restore CS0067
        
        public void Activate()
        {
            _logger.Warning("{method}", nameof(Activate));
        }

        public void On(byte channel)
        {
            _logger.Warning("{method}; channel: {channel}", nameof(On), channel);
        }

        public void OnF(byte channel, uint? deviceId = null)
        {
            _logger.Warning("{method}; channel: {channel}", nameof(OnF), channel);
        }

        public void Off(byte channel)
        {
            _logger.Warning("{method}; channel: {channel}", nameof(Off), channel);
        }

        public void OffF(byte channel, uint? deviceId = null)
        {
            _logger.Warning("{method}; channel: {channel}", nameof(OffF), channel);
        }

        public void Switch(byte channel)
        {
            _logger.Warning("{method}; channel: {channel}", nameof(Switch), channel);
        }

        public void SwitchF(byte channel, uint? deviceId = null)
        {
            _logger.Warning("{method}; channel: {channel}", nameof(SwitchF), channel);
        }

        public void SetBrightness(byte channel, byte brightness)
        {
            _logger.Warning("{method}; channel: {channel}, brightness: {brightness}",
                nameof(SetBrightness), channel, brightness);
        }

        public void SetBrightnessF(byte channel, byte brightness, uint? deviceId = null)
        {
            _logger.Warning("{method}; channel: {channel}, brightness: {brightness}, deviceId: {deviceId}",
                nameof(SetBrightnessF), channel, brightness, deviceId?.ToString() ?? "null");
        }

        public void ReadState(byte channel)
        {
            _logger.Warning("{method}; channel: {channel}", nameof(ReadState), channel);
        }
    }
}