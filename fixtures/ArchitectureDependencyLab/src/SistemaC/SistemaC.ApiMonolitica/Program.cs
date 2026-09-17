using Microsoft.EntityFrameworkCore;
using PacotePrivado.Parametros;
using SistemaC.ApiMonolitica;

var builder = WebApplication.CreateBuilder(args);

// SCENARIO:CFG-001
// SCENARIO:DATA-003
// SCENARIO:DATA-008
var connectionString = builder.Configuration.GetConnectionString("Primary") ?? "Data Source=synthetic_data.db";

// SCENARIO:DATA-001
builder.Services.AddDbContext<SistemaCDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// SCENARIO:CACHE-C-001
builder.Services.AddDistributedMemoryCache();

// SCENARIO:PKG-001
builder.Services.AddSingleton<IParameterProvider, DefaultParameterProvider>();

// SCENARIO:HTTP-C-001
builder.Services.AddHttpClient("ServicoIdentidade", client =>
{
    var baseUrl = builder.Configuration["Services:Identity:BaseUrl"] ?? "http://localhost:5104";
    client.BaseAddress = new Uri(baseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<ServicoIdentidadeClient>();
builder.Services.AddScoped<ServicoDeCache>();
builder.Services.AddScoped<ServicoAutorizacaoComFallback>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program { }
