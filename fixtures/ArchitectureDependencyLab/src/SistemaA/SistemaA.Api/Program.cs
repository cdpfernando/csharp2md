using Microsoft.EntityFrameworkCore;
using PacotePrivado.Parametros;
using SistemaA.Aplicacao;
using SistemaA.Infraestrutura;

var builder = WebApplication.CreateBuilder(args);

// SCENARIO:CFG-001
// SCENARIO:CFG-002
// SCENARIO:CFG-005
// SCENARIO:DATA-003
// SCENARIO:DATA-008
// SCENARIO:NEG-010
// SCENARIO:NEG-012
var primaryConnection = builder.Configuration.GetConnectionString("Primary") 
    ?? "Data Source=synthetic_data.db";
var secondaryConnection = builder.Configuration.GetConnectionString("Secondary");
var declaredPassword = builder.Configuration["Certificates:Password"]; // Nunca lida em runtime exceto verificacao sintetica


// SCENARIO:DATA-001
builder.Services.AddDbContext<SistemaADbContext>(options =>
{
    options.UseSqlite(primaryConnection);
});

// SCENARIO:PKG-001
builder.Services.AddSingleton<IParameterProvider, DefaultParameterProvider>();

// SCENARIO:CFG-004
var serviceKey = "Price";
var priceBaseUrl = builder.Configuration[$"Services:{serviceKey}:BaseUrl"] ?? "http://localhost:5101";

// SCENARIO:HTTP-A-001
builder.Services.AddHttpClient("ServicoPrecos", client =>
{
    client.BaseAddress = new Uri(priceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddScoped<IServicoPrecosClient, ServicoPrecosClient>();
builder.Services.AddScoped<CotacaoRepository>();
builder.Services.AddScoped<SimularCotacaoHandler>();

builder.Services.AddControllers();

// SCENARIO:HOST-001
var app = builder.Build();

app.MapControllers();

app.Run();

public partial class Program { }
