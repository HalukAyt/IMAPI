using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using IMAPI.Api.Data;
using IMAPI.Api.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Authorization;
using IMAPI.Api.Hubs;




var builder = WebApplication.CreateBuilder(args);


// --- Config ---
var jwtSection = builder.Configuration.GetSection("Jwt");
var jwtKey = jwtSection["Key"] ?? throw new InvalidOperationException("Jwt:Key missing");
var jwtIssuer = jwtSection["Issuer"] ?? "ItechMarine";
var jwtAudience = jwtSection["Audience"] ?? "ItechMarineClients";


// --- DB ---
var cs = builder.Configuration.GetConnectionString("DefaultConnection")
?? "Host=localhost;Port=5432;Database=itechmarine;Username=postgres;Password=postgres";


builder.Services.AddDbContext<ItechMarineDbContext>(opt =>
{
    opt.UseNpgsql(cs);
});


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

builder.Services.AddControllers(options =>
{
    var policy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
    options.Filters.Add(new AuthorizeFilter(policy)); // tüm action’lar authorize
});



// --- Services ---
builder.Services.AddSingleton(new TokenService(jwtKey, jwtIssuer, jwtAudience));
builder.Services.AddSingleton<PasswordHasher>();

// --- MQTT ---
builder.Services.Configure<MqttOptions>(builder.Configuration.GetSection("Mqtt"));
builder.Services.AddSingleton<IMqttBridge, MqttBridge>();
builder.Services.AddHostedService(sp => (MqttBridge)sp.GetRequiredService<IMqttBridge>());
builder.Services.AddSignalR();                 // StatusHub kullanıyorsan




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
        Description = "Sadece JWT token'ını gir (Bearer yazma kutusu yoksa: Bearer <token>).",
        Reference = new OpenApiReference
        {
            Type = ReferenceType.SecurityScheme,
            Id = "Bearer"
        }
    };

    c.AddSecurityDefinition("Bearer", jwtScheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        { jwtScheme, Array.Empty<string>() }
    });
});



var app = builder.Build();

// --- Status Hub ---


app.MapHub<StatusHub>("/hubs/status");        // StatusHub varsa



// --- Pipeline ---
app.UseSwagger();
app.UseSwaggerUI();


app.UseAuthentication();
app.UseAuthorization();


app.MapControllers();


app.Run();