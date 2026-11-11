using System.Reflection;
using System.Text.Json.Serialization;
using Hakari.Api.Aplicacao;
using Hakari.Api.Infra;
using Hakari.Api.Workers;
using Hakari.Domain.Interfaces;
using Hakari.Infrastructure;
using Hakari.Infrastructure.Dados;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------- Configuração ----------
var connectionString = config.GetConnectionString("Hakari");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("Defina ConnectionStrings:Hakari (ou a variável ConnectionStrings__Hakari).");

var jwt = config.GetSection("Jwt").Get<JwtOpcoes>() ?? new JwtOpcoes();
if (jwt.Chave.Length < 32)
    throw new InvalidOperationException("Defina Jwt:Chave com pelo menos 32 caracteres (ou a variável Jwt__Chave).");

builder.Services.Configure<JwtOpcoes>(config.GetSection("Jwt"));
builder.Services.Configure<MqttOpcoes>(config.GetSection("Mqtt"));

// ---------- Injeção de dependência ----------
// Cada interface é ligada à sua implementação aqui; os controllers só conhecem as interfaces.
builder.Services.AddInfraestrutura(connectionString);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITokenService, TokenService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IPosteService, PosteService>();
builder.Services.AddScoped<IOcorrenciaService, OcorrenciaService>();
builder.Services.AddScoped<IContaService, ContaService>();
builder.Services.AddScoped<IMonitorService, MonitorService>();

builder.Services.AddHostedService<MqttWorker>();
builder.Services.AddHostedService<IsolamentoWorker>();

// ---------- Web API ----------
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<TratadorDeErros>();
builder.Services.AddHealthChecks().AddCheck<BancoHealthCheck>("postgres");
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Emissor,
            ValidAudience = jwt.Audiencia,
            IssuerSigningKey = jwt.ChaveDeAssinatura(),
            NameClaimType = "name",
            RoleClaimType = "role",
            ClockSkew = TimeSpan.FromMinutes(1)
        };
    });
builder.Services.AddAuthorization();

// ---------- Swagger (documentação automática) ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Hakari API",
        Version = "v1",
        Description = "Iluminação pública que se reporta sozinha. Os nós ESP32 publicam por MQTT; " +
                      "esta API transforma alertas em ocorrências e atende o app do cidadão e da manutenção. " +
                      "Para testar: faça login em /api/auth/login, copie o token e clique em Authorize."
    });
    c.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, $"{Assembly.GetExecutingAssembly().GetName().Name}.xml"));

    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Cole só o token (sem a palavra Bearer)."
    });
    c.OperationFilter<SegurancaSwaggerFiltro>();
});

var app = builder.Build();

// ---------- Banco: migrações e dados iniciais ----------
if (config.GetValue<bool>("Banco:AplicarMigracoes"))
{
    using var escopo = app.Services.CreateScope();
    var db = escopo.ServiceProvider.GetRequiredService<HakariDbContext>();
    await db.Database.MigrateAsync();
    await SemeadorDeDados.SemearAsync(db, escopo.ServiceProvider.GetRequiredService<IHasherDeSenha>(),
                                      config["Seed:SenhaTecnico"], DateTime.UtcNow);
}

// ---------- Pipeline HTTP ----------
app.UseExceptionHandler();
app.UseSwagger();
app.UseSwaggerUI(c => c.DocumentTitle = "Hakari API");
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");
app.MapGet("/", () => Results.Redirect("/swagger")).ExcludeFromDescription();

app.Run();

/// <summary>Exposto para os testes de integração (WebApplicationFactory).</summary>
public partial class Program;
