using MQTTnet.Client.Options;

namespace Nick.Mqtt
{
    public class MqttConfiguration
    {
        public string Server { get; set; } = default!;
        public string? User { get; set; }
        public string? Password { get; set; }
        public string ClientId { get; set; } = default!;
        public bool Tls { get; set; }

        public IMqttClientOptions GetOptions()
        {
            var builder = new MqttClientOptionsBuilder()
                .WithClientId(ClientId)
                .WithTcpServer(Server)
                .WithCleanSession();

            if (Tls)
            {
                builder = builder.WithTls(new MqttClientOptionsBuilderTlsParameters
                {
                    UseTls = true,
                    SslProtocol = System.Security.Authentication.SslProtocols.Tls12,
                    CertificateValidationHandler = _ => true
                });
            }

            if (User != null)
            {
                builder = builder.WithCredentials(User, Password);
            }

            return Configure(builder).Build();
        }

        public virtual MqttClientOptionsBuilder Configure(MqttClientOptionsBuilder builder) => builder;
    }
}
