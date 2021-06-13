using System.Text;
using System.Threading.Tasks;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace NooliteMqttAdapter
{
    public class MqttCommandPublisher
    {
        private readonly IManagedMqttClient _cli;
        public const string TurnOff = "0";
        public const string TurnOn = "1";
        
        public static readonly Encoding Encoding = Encoding.UTF8;

        public MqttCommandPublisher(IManagedMqttClient cli)
        {
            _cli = cli;
        }
        
        public async Task PublishAsync(string topic, string command)
        {
            var message = new ManagedMqttApplicationMessageBuilder()
                .WithApplicationMessage(new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithRetainFlag()
                    .WithPayload(Encoding.GetBytes(command))
                    .WithAtLeastOnceQoS()
                    .Build())
                .Build();

            await _cli.PublishAsync(message);
        }
        
        public async Task PublishTurnOffAsync(string topic)
        {
            await PublishAsync(topic, TurnOff);
        }
        
        public async Task PublishTurnOnAsync(string topic)
        {
            await PublishAsync(topic, TurnOn);
        }
    }
}