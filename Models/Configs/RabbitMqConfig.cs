using System.Collections.Generic;

namespace Consumer.Models.Configs
{
    public class RabbitMqConfig
    {
        public string HostName { get; set; }
        public int Port { get; set; }
        public string UserName { get; set; }
        public string Password { get; set; }
        public string VirtualHost { get; set; }
        public bool UseTls { get; set; }
        public string ExchangeName { get; set; }
        public string QueueName { get; set; }
        public string RoutingKey { get; set; }
        public string DeadLetterExchange { get; set; }
        public string DeadLetterQueue { get; set; }
        public ConnectionSettings ConnectionSettings { get; set; }
        public Dictionary<string, object> QueueArguments { get; set; }
    }

    public class ConnectionSettings
    {
        public bool AutomaticRecoveryEnabled { get; set; }
        public int NetworkRecoveryIntervalSeconds { get; set; }
        public int RequestedConnectionTimeoutSeconds { get; set; }
        public int RequestedHeartbeatSeconds { get; set; }
        public string ClientProvidedNamePrefix { get; set; }
    }
}