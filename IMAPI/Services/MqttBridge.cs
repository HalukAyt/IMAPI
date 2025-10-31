// Services/MqttBridge.cs
using System.Text.Json;
using MQTTnet;
using MQTTnet.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.SignalR;
using IMAPI.Api.Data;
using IMAPI.Api.Entities;
using IMAPI.Api.Hubs;          // StatusHub kullanıyorsan
using Microsoft.Extensions.Logging;

namespace IMAPI.Api.Services;

public class MqttOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 1883;
    public bool UseTls { get; set; } = false;
    public string ClientId { get; set; } = "imapi-backend";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool CleanSession { get; set; } = true;
    public int KeepAliveSeconds { get; set; } = 30;
    public string BaseTopic { get; set; } = "itechmarine";
}

public interface IMqttBridge
{
    Task PublishCommandAsync(string deviceSerial, object payload, CancellationToken ct = default);
    bool IsConnected { get; }
}

public sealed class MqttBridge : BackgroundService, IMqttBridge
{
    private readonly ILogger<MqttBridge> _logger;
    private readonly IServiceProvider _sp;
    private readonly MqttOptions _opt;
    private IMqttClient? _client;

    public bool IsConnected => _client?.IsConnected == true;

    public MqttBridge(
        ILogger<MqttBridge> logger,
        IOptions<MqttOptions> opt,
        IServiceProvider sp)
    {
        _logger = logger;
        _sp = sp;
        _opt = opt.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var factory = new MqttFactory();
        _client = factory.CreateMqttClient();

        _client.DisconnectedAsync += async e =>
        {
            _logger.LogWarning("MQTT disconnected: Reason={Reason}, ClientWasConnected={WasConnected}",
                e.Reason, e.ClientWasConnected);
            await Task.Delay(TimeSpan.FromSeconds(3), stoppingToken);
            try { await ConnectAndSubscribeAsync(stoppingToken); }
            catch (Exception ex) { _logger.LogError(ex, "MQTT reconnect failed"); }
        };

        _client.ApplicationMessageReceivedAsync += async e =>
        {
            try
            {
                var topic = e.ApplicationMessage.Topic ?? string.Empty;
                var payload = e.ApplicationMessage.ConvertPayloadToString();

                _logger.LogInformation("MQTT RX {Topic}", topic);
                await HandleIncomingAsync(topic, payload, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling MQTT message");
            }
        };

        await ConnectAndSubscribeAsync(stoppingToken);
        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task ConnectAndSubscribeAsync(CancellationToken ct)
    {
        if (_client is null) throw new InvalidOperationException("MQTT client is null");

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(_opt.ClientId)
            .WithCleanSession(_opt.CleanSession)
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_opt.KeepAliveSeconds))
            .WithTcpServer(_opt.Host, _opt.Port);

        if (!string.IsNullOrWhiteSpace(_opt.Username))
            builder = builder.WithCredentials(_opt.Username, _opt.Password);

        if (_opt.UseTls)
            builder = builder.WithTlsOptions(o => { o.UseTls(); });

        var options = builder.Build();

        await _client.ConnectAsync(options, ct);
        _logger.LogInformation("MQTT connected to {Host}:{Port}", _opt.Host, _opt.Port);

        var statusTopic = $"{_opt.BaseTopic}/device/+/status";
        var ackTopic = $"{_opt.BaseTopic}/device/+/ack";

        await _client.SubscribeAsync(statusTopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, ct);
        await _client.SubscribeAsync(ackTopic, MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce, ct);

        _logger.LogInformation("MQTT subscribed: {Status} , {Ack}", statusTopic, ackTopic);
    }

    public async Task PublishCommandAsync(string deviceSerial, object payload, CancellationToken ct = default)
    {
        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException("MQTT not connected");

        var topic = $"{_opt.BaseTopic}/device/{deviceSerial}/cmd";
        var json = JsonSerializer.Serialize(payload);

        var msg = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(false)
            .Build();

        await _client.PublishAsync(msg, ct);
        _logger.LogInformation("MQTT TX {Topic} {Payload}", topic, json);
    }

    private async Task HandleIncomingAsync(string topic, string payload, CancellationToken ct)
    {
        // Beklenen: itechmarine/device/{serial}/(status|ack)
        var parts = topic.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return;

        var root = parts[0];
        var entity = parts[1];
        var serial = parts[2];
        var kind = parts[3];

        if (!string.Equals(root, _opt.BaseTopic, StringComparison.OrdinalIgnoreCase))
            return;
        if (!string.Equals(entity, "device", StringComparison.OrdinalIgnoreCase))
            return;

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ItechMarineDbContext>();
        IHubContext<StatusHub>? hub = null;
        try
        {
            hub = scope.ServiceProvider.GetService<IHubContext<StatusHub>>();
        }
        catch { /* SignalR opsiyonel olabilir */ }

        // Telemetry kaydı
        await PersistTelemetryAsync(db, serial, payload, ct);

        switch (kind)
        {
            case "status":
                await ProcessStatusAsync(db, hub, serial, payload, ct);
                break;
            case "ack":
                await PushAckAsync(db, hub, serial, payload, ct);
                break;
        }
    }

    private static async Task PersistTelemetryAsync(ItechMarineDbContext db, string serial, string payload, CancellationToken ct)
    {
        try
        {
            var dev = await db.Devices.FirstOrDefaultAsync(d => d.Serial == serial, ct);
            var tel = new Telemetry
            {
                DeviceSerial = serial,
                DeviceId = dev?.Id,
                Ts = DateTime.UtcNow,
                Payload = payload
            };
            db.Telemetries.Add(tel);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            // Telemetry entity yoksa sessiz geç
        }
    }

    private static string BoatGroup(Guid boatId) => $"boat-{boatId}";

    private static async Task ProcessStatusAsync(
        ItechMarineDbContext db,
        IHubContext<StatusHub>? hub,
        string serial,
        string payload,
        CancellationToken ct)
    {
        var device = await db.Devices.Include(d => d.Boat).FirstOrDefaultAsync(d => d.Serial == serial, ct);
        if (device is null) return;

        // [ADDED] LastSeen güncelle
        device.LastSeen = DateTime.UtcNow;

        // relays: [{ch, state}] varsa LightChannel güncelle
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("relays", out var relays) &&
                relays.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in relays.EnumerateArray())
                {
                    if (!r.TryGetProperty("ch", out var chEl) || !r.TryGetProperty("state", out var stEl))
                        continue;

                    var ch = chEl.GetInt32();
                    var st = stEl.GetInt32(); // 0/1

                    var lc = await db.LightChannels
                        .FirstOrDefaultAsync(x => x.DeviceId == device.Id && x.ChNo == ch, ct);
                    if (lc != null)
                    {
                        lc.IsOn = st == 1;
                        lc.UpdatedAt = DateTime.UtcNow;
                    }
                }
                await db.SaveChangesAsync(ct);
            }
            else
            {
                // Sadece LastSeen güncellemesi için de kaydet
                await db.SaveChangesAsync(ct);
            }
        }
        catch
        {
            // JSON parse hatası sessiz geç, yine de LastSeen'i yaz
            await db.SaveChangesAsync(ct);
        }

        // Canlı yayın (SignalR)
        if (hub is not null && device.BoatId != Guid.Empty)
        {
            await hub.Clients.Group(BoatGroup(device.BoatId))
                .SendAsync("status", new { serial, payload }, ct);
        }
    }

    private static async Task PushAckAsync(
        ItechMarineDbContext db,
        IHubContext<StatusHub>? hub,
        string serial,
        string payload,
        CancellationToken ct)
    {
        var device = await db.Devices.Include(d => d.Boat).FirstOrDefaultAsync(d => d.Serial == serial, ct);

        // [ADDED] PendingCommands → delivered
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("id", out var idEl) &&
                idEl.ValueKind == JsonValueKind.String)
            {
                var idStr = idEl.GetString();
                if (!string.IsNullOrWhiteSpace(idStr) && Guid.TryParse(idStr, out var gid))
                {
                    var pc = await db.PendingCommands
                        .FirstOrDefaultAsync(x => x.Id == gid && x.DeviceSerial == serial, ct);
                    if (pc != null)
                    {
                        pc.Status = "delivered";
                        pc.DeliveredAt = DateTime.UtcNow;
                        await db.SaveChangesAsync(ct);
                    }
                }
            }
        }
        catch
        {
            // ack parse edilemedi, sessiz geç
        }

        // SignalR yayın
        if (device is not null && hub is not null && device.BoatId != Guid.Empty)
        {
            await hub.Clients.Group(BoatGroup(device.BoatId))
                .SendAsync("ack", new { serial, payload }, ct);
        }
    }
}
