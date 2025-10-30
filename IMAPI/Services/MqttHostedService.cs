namespace IMAPI.Services;

using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Packets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using IMAPI.Realtime;

public class MqttOptions
{
    public string Server { get; set; } = "broker.hivemq.com";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string BaseTopic { get; set; } = "itech";
}

public interface IMqttPublisher
{
    Task PublishCommandAsync(string deviceId, string jsonPayload, CancellationToken ct = default);
}

public class MqttHostedService : BackgroundService, IMqttPublisher
{
    private readonly ILogger<MqttHostedService> _log;
    private readonly MqttOptions _opt;
    private readonly IHubContext<AppHub> _hub;
    private IMqttClient? _client;

    public MqttHostedService(ILogger<MqttHostedService> log, IOptions<MqttOptions> opt, IHubContext<AppHub> hub)
    { _log = log; _opt = opt.Value; _hub = hub; }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory(); // NOTE: requires using MQTTnet; package added in csproj
        _client = factory.CreateMqttClient();

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            var topic = e.ApplicationMessage.Topic ?? string.Empty;
            var payload = e.ApplicationMessage.PayloadSegment;
            var text = payload.Array is null || payload.Count == 0 ? string.Empty : System.Text.Encoding.UTF8.GetString(payload.Array, payload.Offset, payload.Count);
            _log.LogInformation("MQTT Rx {Topic} {Len}", topic, payload.Count);
            // Push to clients if necessary
            await _hub.Clients.All.SendAsync("mqtt", new { topic, text });
        };

        _client.DisconnectedAsync += async e =>
        {
            _log.LogWarning("MQTT disconnected. Reconnecting in 3s...");
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            try { await ConnectAsync(stoppingToken); } catch (Exception ex) { _log.LogError(ex, "Reconnect failed"); }
        };

        await ConnectAsync(stoppingToken);
    }

    private async Task ConnectAsync(CancellationToken ct)
    {
        if (_client is null) return;
        var options = new MqttClientOptionsBuilder()
            .WithTcpServer(_opt.Server, _opt.Port)
            .WithCleanSession()
            .Build();
        if (!string.IsNullOrEmpty(_opt.Username))
        {
            options = new MqttClientOptionsBuilder()
                .WithTcpServer(_opt.Server, _opt.Port)
                .WithCredentials(_opt.Username, _opt.Password)
                .WithCleanSession()
                .Build();
        }

        await _client.ConnectAsync(options, ct);
        var baseTopic = _opt.BaseTopic;
        // Subscribe to acks & status from devices
        await _client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter($"{baseTopic}/+/ack")
            .WithTopicFilter($"{baseTopic}/+/status")
            .Build(), ct);
    }

    public async Task PublishCommandAsync(string deviceId, string jsonPayload, CancellationToken ct = default)
    {
        if (_client is null || !_client.IsConnected) throw new InvalidOperationException("MQTT not connected");
        var msg = new MqttApplicationMessageBuilder()
            .WithTopic($"{_opt.BaseTopic}/{deviceId}/cmd")
            .WithPayload(jsonPayload)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();
        await _client.PublishAsync(msg, ct);
    }
}