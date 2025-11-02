using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using IMAPI.Api.Data;
using IMAPI.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Mvc;
using IMAPI.Api.Hubs;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// *** ÖNEMLİ: Kestrel tüm arayüzlerde 5074 portunu dinlesin (LAN’dan erişim) ***
builder.WebHost.UseUrls("http://0.0.0.0:5074");
// Alternatif/ekstra (istemiyorsan gerekmez):
// builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(5074));


// --- Config ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
var jwtIssuer = jwtSection["Issuer"] ?? "ItechMarine";
var jwtAudience = jwtSection["Audience"] ?? "ItechMarineClients";

// --- DB ---
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
         ?? "Host=localhost;Port=5432;Database=itechmarine;Username=postgres;Password=postgres";

builder.Services.AddDbContext<ItechMarineDbContext>(opt => opt.UseNpgsql(cs));

// --- Auth ---
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// *** CORS: Tarayıcı istemcileri için. (ESP32 için gerekmez ama kalsın) ***
builder.Services.AddCors(opt =>
{
    opt.AddPolicy("AllowLan", p => p
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod());
});

// --- Controllers ---
// Global Authorize: Tüm action’lar authenticated ister. (Cihaz uçlarını [AllowAnonymous] ile açacağız)
builder.Services
    .AddControllers(options =>
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        options.Filters.Add(new AuthorizeFilter(policy));
    })
    .AddJsonOptions(o =>
    {
        // ESP32 / farklı casing sorunlarına karşı toleranslı
        o.JsonSerializerOptions.PropertyNameCaseInsensitive = true;
    });

// --- Services ---
builder.Services.AddSingleton(new TokenService(jwtKey, jwtIssuer, jwtAudience));
// Eğer kendi PasswordHasher sınıfın varsa kalsın; yoksa Microsoft’un generic hasher’ını kullan:
builder.Services.AddSingleton<IPasswordHasher<object>, Microsoft.AspNetCore.Identity.PasswordHasher<object>>();

// --- MQTT ---
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<IMqttBridge, MqttBridge>();
builder.Services.AddHostedService(sp => (MqttBridge)sp.GetRequiredService<IMqttBridge>());
builder.Services.AddScoped<PasswordHasher>();

// --- SignalR (opsiyonel) ---
builder.Services.AddSignalR();

// --- Swagger ---
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "ItechMarine API", Version = "v1" });

    var jwtScheme = new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Sadece JWT token'ını gir. Format: Bearer {token}",
        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
    };

    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { jwtScheme, Array.Empty<string>() } });
});

var app = builder.Build();

// --- Swagger UI (root’ta) ---
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "ItechMarine API v1");
    c.RoutePrefix = "swagger"; // <-- tekrar /swagger altına alır
});


// --- Middleware ---
app.UseCors("AllowLan");
app.UseAuthentication();
app.UseAuthorization();

// --- Hubs (opsiyonel) ---
app.MapHub<StatusHub>("/hubs/status");

// --- Health ping (cihaz & canlılık testi için, yetkisiz açık) ---
app.MapGet("/health", () => Results.Ok(new { ok = true, time = DateTime.UtcNow }))
   .AllowAnonymous();

// --- Controllers ---
app.MapControllers();

app.Run();
