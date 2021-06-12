using System.Text;
using MQTTnet;
using MQTTnet.Extensions.ManagedClient;

namespace NooliteMqttAdapter
{
    public static class MqttCommands
    {
        public const string TurnOff = "0";
        public const string TurnOn = "1";
        
        public static readonly Encoding Encoding = Encoding.UTF8;

        public static ManagedMqttApplicationMessage Create(string topic, string command)
        {
            return new ManagedMqttApplicationMessageBuilder()
                .WithApplicationMessage(new MqttApplicationMessageBuilder()
                    .WithTopic(topic)
                    .WithRetainFlag()
                    .WithPayload(Encoding.GetBytes(command))
                    .WithAtLeastOnceQoS()
                    .Build())
                .Build();
        }
        
        public static ManagedMqttApplicationMessage CreateTurnOff(string topic)
        {
            return Create(topic, TurnOff);
        }
        
        public static ManagedMqttApplicationMessage CreateTurnOn(string topic)
        {
            return Create(topic, TurnOn);
        }
    }
}