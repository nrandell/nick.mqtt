using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Disconnecting;
using MQTTnet.Client.Options;
using MQTTnet.Client.Publishing;
using MQTTnet.Client.Subscribing;
using MQTTnet.Exceptions;

using Polly;
using Polly.Retry;

namespace Nick.Mqtt
{
    public class ReliableMqttClient<TConfiguration>
        where TConfiguration : MqttConfiguration
    {
        public ILogger Logger { get; }
        public IHostApplicationLifetime ApplicationLifetime { get; }
        public IMqttClient Client { get; }
        public IMqttClientOptions Options { get; }
        public TConfiguration Configuration { get; }

        private readonly AsyncRetryPolicy _policy;

        public MqttClientSubscribeOptions? SubscriptionOptions { get; protected set; }

        private readonly ForeverJitter _foreverJitter;
        private readonly JitterBackoff _jitterBackoff;

        protected virtual void OnConnected(MqttClientAuthenticateResult connected) { }
        protected virtual void OnSubscribed(MqttClientSubscribeResult subscription) { }
        protected virtual void OnDisconnected(MqttClientDisconnectedEventArgs ev) { }
        protected virtual void HandleError(MqttApplicationMessage applicationMessage, Exception ex) { }

        protected ReliableMqttClient(ILogger logger, IMqttFactory factory, TConfiguration configuration, IHostApplicationLifetime applicationLifetime)
        {
            if (factory == null) throw new ArgumentNullException(nameof(factory));
            if (configuration == null) throw new ArgumentNullException(nameof(configuration));

            _foreverJitter = new ForeverJitter(TimeSpan.FromSeconds(1), 10, TimeSpan.FromSeconds(60));
            _jitterBackoff = new JitterBackoff(_foreverJitter);

            Logger = logger;
            ApplicationLifetime = applicationLifetime;
            Client = factory.CreateMqttClient();
            Options = configuration.GetOptions();
            Configuration = configuration;

            _policy = Policy.Handle<MqttCommunicationException>()
                .WaitAndRetryAsync(_foreverJitter);
        }

        public async Task StartAsync(CancellationToken stoppingToken)
        {
            var client = Client;
            client.UseApplicationMessageReceivedHandler(HandleMessage);
            client.UseDisconnectedHandler(HandleDisconnected);
            client.UseConnectedHandler(HandleConnected);

            await Connect(stoppingToken).ConfigureAwait(false);
        }

        private Task HandleConnected(MqttClientConnectedEventArgs arg)
        {
            Logger.LogInformation("Client connected: {@Reason}", arg.AuthenticateResult);
            _jitterBackoff.Clear();
            OnConnected(arg.AuthenticateResult);
            return Task.CompletedTask;
        }

        protected virtual Task HandleMessage(MqttApplicationMessageReceivedEventArgs ev) => Task.CompletedTask;

        private async Task HandleDisconnected(MqttClientDisconnectedEventArgs ev)
        {
            Logger.LogWarning(ev.Exception, "Client disconnected: {WasConnected} {Exception}", ev.ClientWasConnected, ev.Exception.Message);
            var delay = _jitterBackoff.Next();
            await Task.Delay(delay, ApplicationLifetime.ApplicationStopping).ConfigureAwait(false);
            await Connect(ApplicationLifetime.ApplicationStopping).ConfigureAwait(false);
        }

        private async Task Connect(CancellationToken stoppingToken)
        {
            try
            {
                await Client.ConnectAsync(Options, stoppingToken).ConfigureAwait(false);
                Logger.LogInformation("Connected");
                var subscriptionOptions = SubscriptionOptions;
                if (subscriptionOptions != null)
                {
                    var subscription = await Client.SubscribeAsync(SubscriptionOptions, stoppingToken).ConfigureAwait(false);
                    Logger.LogInformation("Subscribed");
                    OnSubscribed(subscription);
                }
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Logger.LogWarning(ex, "Failed to connect: {Exception}", ex.Message);
            }
        }

        public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage message, CancellationToken stoppingToken) =>
            _policy.ExecuteAsync(ct => Client.PublishAsync(message, ct), stoppingToken);
    }
}
