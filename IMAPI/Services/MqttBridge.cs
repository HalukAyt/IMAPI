    // Services/MqttBridge.cs
    using System.Text.Json;
    using MQTTnet;
    using MQTTnet.Client;
    using MQTTnet.Protocol;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Options;
    using Microsoft.AspNetCore.SignalR;
    using IMAPI.Api.Data;
    using IMAPI.Api.Entities;
    using IMAPI.Api.Hubs;
    using Microsoft.Extensions.Logging;
    using MQTTnet.Packets;

using MQTTnet.Extensions.ManagedClient;

namespace IMAPI.Api.Services;

    public class MqttOptions
    {
        public string Host { get; set; } = "localhost";
        public int Port { get; set; } = 1883;
        public bool UseTls { get; set; } = false;

        /// <summary>
        /// Her ortam için BENZERSİZ ve SABİT bir ClientId ver (örn: imapi-bridge-prod).
        /// Aynı ClientId birden fazla yerde kullanılırsa broker eski bağlantıyı düşürür.
        /// </summary>
        public string ClientId { get; set; } = "imapi-bridge-dev";

        public string? Username { get; set; }
        public string? Password { get; set; }

        /// <summary>
        /// Kalıcı oturum için false önerilir.
        /// </summary>
        public bool CleanSession { get; set; } = false;

        public int KeepAliveSeconds { get; set; } = 30;

        /// <summary>
        /// Managed client otomatik yeniden bağlanma gecikmesi (sn)
        /// </summary>
        public int AutoReconnectSeconds { get; set; } = 5;

        /// <summary>
        /// MQTT v5 Session Expiry (sn). Örn 86400 = 24 saat.
        /// </summary>
        public uint SessionExpirySeconds { get; set; } = 86400;

        public string BaseTopic { get; set; } = "itechmarine";
    }

    public interface IMqttBridge
    {
        Task PublishCommandAsync(string deviceSerial, object payload, CancellationToken ct = default);
        bool IsConnected { get; }
    }

    /// <summary>
    /// HiveMQ Cloud (TLS: 8883) ve benzer broker’larla stabil çalışan,
    /// kalıcı oturum + otomatik reconnect + QoS1 kullanan Managed MQTT köprüsü.
    /// </summary>
    public sealed class MqttBridge : BackgroundService, IMqttBridge
    {
        private readonly ILogger<MqttBridge> _logger;
        private readonly IServiceProvider _sp;
        private readonly MqttOptions _opt;

        private IManagedMqttClient? _client;

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
            _client = factory.CreateManagedMqttClient();

            // Mesaj alımı
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

        // Bağlandı
        _client.ConnectedAsync += async e =>
        {
            _logger.LogInformation("MQTT connected (Server={Server}:{Port})", _opt.Host, _opt.Port);

            var status = new MqttTopicFilterBuilder()
                .WithTopic($"{_opt.BaseTopic}/device/+/status")
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            var ack = new MqttTopicFilterBuilder()
                .WithTopic($"{_opt.BaseTopic}/device/+/ack")
                .WithQualityOfServiceLevel(MQTTnet.Protocol.MqttQualityOfServiceLevel.AtLeastOnce)
                .Build();

            // 🔧 Burayı böyle çağır:
            await _client.SubscribeAsync(new[] { status, ack });

            _logger.LogInformation("MQTT subscribed: {Status} , {Ack}", status.Topic, ack.Topic);
        };

        // Koptu (Managed client kendi reconnect eder, burada SAKİN kalıyoruz)
        _client.DisconnectedAsync += async e =>
            {
                _logger.LogWarning("MQTT disconnected. Reason={Reason}", e.ReasonString);
                await Task.CompletedTask;
            };

            // Client seçenekleri (MQTT v5 + kalıcı oturum + TLS)
            var clientOptions = new MqttClientOptionsBuilder()
                .WithClientId(_opt.ClientId)
                .WithTcpServer(_opt.Host, _opt.Port)
                .WithKeepAlivePeriod(TimeSpan.FromSeconds(_opt.KeepAliveSeconds))
                .WithProtocolVersion(MQTTnet.Formatter.MqttProtocolVersion.V500)
                .WithCleanSession(_opt.CleanSession) // kalıcı session için false
                .WithSessionExpiryInterval(_opt.SessionExpirySeconds);

            if (!string.IsNullOrWhiteSpace(_opt.Username))
                clientOptions = clientOptions.WithCredentials(_opt.Username, _opt.Password);

            if (_opt.UseTls)
            {
                clientOptions = clientOptions.WithTlsOptions(o =>
                {
                    o.UseTls();
                    o.WithCertificateValidationHandler(_ => true); // public CA kullanıyorsan kaldırılabilir; burada default kalsın
                    // Not: MQTTnet TLS içinde SNI destekliyor, ekstra ayar gerekmiyor.
                });
            }

            var managedOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(clientOptions.Build())
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(_opt.AutoReconnectSeconds))
                .Build();

            // Başlat
            await _client.StartAsync(managedOptions);

            // Servis yaşam döngüsü boyunca bekle
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }

        public async Task PublishCommandAsync(string deviceSerial, object payload, CancellationToken ct = default)
        {
            if (_client is null)
                throw new InvalidOperationException("MQTT client not initialized");

            var topic = $"{_opt.BaseTopic}/device/{deviceSerial}/cmd";
            var json = JsonSerializer.Serialize(payload);

            var msg = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(json)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce) // QoS1
                .WithRetainFlag(false)
                .Build();

            // Managed client kuyruğa alır; offline ise bağlanınca gönderir
            await _client.EnqueueAsync(msg);
            _logger.LogInformation("MQTT TX {Topic} {Payload}", topic, json);
        }

        // ==== RX işlemleri ====

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
            try { hub = scope.ServiceProvider.GetService<IHubContext<StatusHub>>(); }
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

            // LastSeen güncelle
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
                    await db.SaveChangesAsync(ct);
                }
            }
            catch
            {
                // JSON parse hatası; yine de LastSeen’i kaydedelim
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

            // PendingCommands → delivered
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
                // ack parse edilemedi
            }

            // SignalR yayın
            if (device is not null && hub is not null && device.BoatId != Guid.Empty)
            {
                await hub.Clients.Group(BoatGroup(device.BoatId))
                    .SendAsync("ack", new { serial, payload }, ct);
            }
        }
    }
