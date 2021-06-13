using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Serilog;
using ThinkingHome.NooLite;
using ThinkingHome.NooLite.Internal;

namespace NooliteMqttAdapter.NooliteAdapter
{
    internal class AdapterWrapper : IDisposable, IMtrfAdapter
    {
        private readonly int _delayMs;
        private readonly CancellationTokenSource _cts;
        private readonly ILogger _logger;
        
        private MTRFXXAdapter Adapter { get; }
        private readonly object _lock = new ();
        private readonly ConcurrentQueue<Action> _actionQueue = new();
        private readonly ManualResetEventSlim _resetEvent = new (false);
        
        public AdapterWrapper(string portName, int delayMs, ILogger logger, CancellationTokenSource cts)
        {
            _delayMs = delayMs;
            _cts = cts;
            Adapter = new MTRFXXAdapter(portName);
            _logger = logger.ForContext<MTRFXXAdapter>();
        }

        protected string DeviceName => typeof(MTRFXXAdapter).ToString();

        public event EventHandler<ReceivedData>? ReceiveData;

        private async Task ProcessingLoop()
        {
            while (!_cts.IsCancellationRequested)
            {
                _resetEvent.Reset();
                if (_actionQueue.TryDequeue(out var action))
                {
                    action();
                    await Task.Delay(TimeSpan.FromMilliseconds(_delayMs));
                }
                else
                {
                    _resetEvent.Wait(_cts.Token);
                }
            }
        }

        private void EnqueueAction(Action action)
        {
            _actionQueue.Enqueue(action);
            _resetEvent.Set();
        }
        
        void IMtrfAdapter.Activate()
        {
            EnsureAdapterConnected();
        }

        void IMtrfAdapter.On(byte channel)
        {
            EnqueueAction(() => Adapter.On(channel));
        }

        void IMtrfAdapter.OnF(byte channel, uint? deviceId)
        {
            EnqueueAction(() => Adapter.OnF(channel, deviceId));
        }

        void IMtrfAdapter.Off(byte channel)
        {
            EnqueueAction(() => Adapter.Off(channel));
        }

        void IMtrfAdapter.OffF(byte channel, uint? deviceId)
        {
            EnqueueAction(() => Adapter.OffF(channel, deviceId));
        }

        void IMtrfAdapter.Switch(byte channel)
        {
            EnqueueAction(() => Adapter.Switch(channel));
        }

        void IMtrfAdapter.SwitchF(byte channel, uint? deviceId)
        {
            EnqueueAction(() => Adapter.SwitchF(channel, deviceId));
        }

        void IMtrfAdapter.SetBrightness(byte channel, byte brightness)
        {
            EnqueueAction(() => Adapter.SetBrightness(channel, brightness));
        }

        void IMtrfAdapter.SetBrightnessF(byte channel, byte brightness, uint? deviceId)
        {
            EnqueueAction(() => Adapter.SetBrightnessF(channel, brightness, deviceId));
        }
        
        void IMtrfAdapter.ReadState(byte channel)
        {
            EnqueueAction(() => Adapter.SendCommand(MTRFXXMode.TXF, MTRFXXAction.SendCommand, channel, MTRFXXCommand.ReadState));
        }

        private void EnsureAdapterConnected()
        {
            if (Adapter.IsOpened)
                return;

            lock (_lock)
            {
                if (Adapter.IsOpened)
                    return;
                
                Adapter.Connect += OnConnect;
                Adapter.Disconnect += OnDisconnect;
                Adapter.ReceiveData += OnReceiveData;
                Adapter.ReceiveMicroclimateData += OnReceiveMicroclimateData;
                Adapter.Error += OnError;
                Adapter.Open();
                Adapter.ExitServiceMode();
                Task.Run(ProcessingLoop, _cts.Token);
            }
        }

        private void OnError(object sender, Exception ex)
        {
            _logger.Error(ex, "Ошибка в работе адаптера!");
        }

        private void OnReceiveMicroclimateData(object sender, MicroclimateData data)
        {
            _logger.Debug($"Получены климатические данные: {data}");
            
            RaiseDataReceived(data);
        }

        private void OnReceiveData(object sender, ReceivedData data)
        {
            if (data.Command == MTRFXXCommand.MicroclimateData)
            {
                return;
            }
            
            _logger.Debug($"Получены данные: {JsonConvert.SerializeObject(data, Formatting.Indented, new StringEnumConverter())}");
            
            RaiseDataReceived(data);
        }

        private void RaiseDataReceived(ReceivedData data)
        {
            var handler = Interlocked.CompareExchange(ref ReceiveData, null, null);
            handler?.Invoke(this, data);
        }

        private void OnDisconnect(object obj)
        {
            _logger.Warning("Адаптер отключен");
        }

        private void OnConnect(object obj)
        {
            _logger.Information("Адаптер подключен");
        }

        public virtual void Dispose()
        {
            if (Adapter.IsOpened)
            {
                Adapter.Connect -= OnConnect;
                Adapter.Disconnect -= OnDisconnect;
                Adapter.ReceiveData -= OnReceiveData;
                Adapter.ReceiveMicroclimateData -= OnReceiveMicroclimateData;
                Adapter.Error -= OnError;
            }
            
            Adapter.Dispose();
        }
    }
}