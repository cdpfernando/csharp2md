using Microsoft.EntityFrameworkCore;
using PacotePrivado.Parametros;
using SistemaD.Api;
using SistemaD.Aplicacao;
using SistemaD.Infraestrutura;

var builder = WebApplication.CreateBuilder(args);

// SCENARIO:CFG-001
// SCENARIO:DATA-003
// SCENARIO:DATA-008
var connectionString = builder.Configuration.GetConnectionString("Primary") ?? "Data Source=synthetic_data.db";

// SCENARIO:DATA-001
builder.Services.AddDbContext<SistemaDDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// Cache distribuido em memoria
builder.Services.AddDistributedMemoryCache();

// SCENARIO:PKG-001
builder.Services.AddSingleton<IParameterProvider, DefaultParameterProvider>();

// SCENARIO:MSG-D-001
// Canal de lote de preco em memoria
builder.Services.AddSingleton<LoteDePrecoChannel>();

// SCENARIO:MSG-D-002
// Consumidor em background service
builder.Services.AddHostedService<LoteDePrecoBackgroundProcessor>();

// SCENARIO:HTTP-D-001
builder.Services.AddHttpClient("ServicoCalculo", client =>
{
    var baseUrl = builder.Configuration["Services:Calculation:BaseUrl"] ?? "http://localhost:5105";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<PrecoRepository>();
builder.Services.AddScoped<ServicoCalculoClient>();
builder.Services.AddScoped<CalcularPrecoUseCase>();

// SCENARIO:CFG-003
#if CONDITIONAL_AUTH
// Autenticação e telemetria ativas sob símbolo condicional
builder.Services.AddAuthentication("ProductionBearerScheme");
builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddConsole();
});
#else
// Autenticação e telemetria em modo padrão/desenvolvimento sob ausência do símbolo condicional
builder.Services.AddAuthentication("DevFallbackScheme");
builder.Services.AddLogging(logging =>
{
    logging.AddDebug();
});
#endif

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program { }
