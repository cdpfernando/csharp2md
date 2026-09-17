using Microsoft.EntityFrameworkCore;
using PacotePrivado.Broker;
using PacotePrivado.Parametros;
using SistemaE.Copia.Aplicacao;
using SistemaE.Copia.Formalizacao;
using SistemaE.Copia.Identificacao;
using SistemaE.Copia.Infraestrutura;
using SistemaE.Copia.Oferta;
using SistemaE.Copia.PosProcessamento;
using SistemaE.Copia.Validacao;

var builder = WebApplication.CreateBuilder(args);

// SCENARIO:CFG-001
// SCENARIO:DATA-003
// SCENARIO:DATA-008
var connectionString = builder.Configuration.GetConnectionString("Primary") ?? "Data Source=synthetic_data.db";

// SCENARIO:DATA-001
builder.Services.AddDbContext<SistemaEDbContext>(options =>
{
    options.UseSqlite(connectionString);
});

// SCENARIO:PKG-001
builder.Services.AddSingleton<IParameterProvider, DefaultParameterProvider>();

// SCENARIO:PKG-002
builder.Services.AddSingleton<IEventBus, HostInMemoryEventBusECopia>();

// SCENARIO:CFG-001
// SCENARIO:MSG-GAP-002
builder.Services.Configure<BrokerOptions>(options =>
{
    options.Brokers = builder.Configuration["Messaging:Brokers"] ?? "localhost:9092";
    options.Topic = builder.Configuration["Messaging:Topic"] ?? "vendas-topic";
    options.SchemaRegistryUrl = builder.Configuration["Messaging:SchemaRegistryUrl"] ?? "http://localhost:8081";
    options.ClientPassword = builder.Configuration["Messaging:ClientPassword"] ?? "NOT_A_REAL_SECRET_ROTATE_ME";
});

// SCENARIO:HTTP-E-001
builder.Services.AddHttpClient("ServicoCatalogo", client =>
{
    var baseUrl = builder.Configuration["Services:Catalog:BaseUrl"] ?? "http://localhost:5102";
    client.BaseAddress = new Uri(baseUrl);
});

// SCENARIO:HTTP-E-002
builder.Services.AddHttpClient("ServicoFormalizacao", client =>
{
    var baseUrl = builder.Configuration["Services:Formalization:BaseUrl"] ?? "http://localhost:5106";
    client.BaseAddress = new Uri(baseUrl);
});

builder.Services.AddScoped<VendaRepository>();
builder.Services.AddScoped<SharedInfraAuditService>();
builder.Services.AddScoped<ServicoCatalogoClient>();
builder.Services.AddScoped<ServicoFormalizacaoClient>();

builder.Services.AddScoped<IdentificacaoModulo>();
builder.Services.AddScoped<OfertaModulo>();
builder.Services.AddScoped<ValidacaoModulo>();
builder.Services.AddScoped<PosProcessamentoModulo>();
builder.Services.AddScoped<FormalizacaoModulo>();

builder.Services.AddScoped<OrquestradorDeVenda>();
builder.Services.AddScoped<PedidoRecebidoIntegrationEventHandler>();

builder.Services.AddControllers();

var app = builder.Build();

app.MapControllers();

app.Run();

public class HostInMemoryEventBusECopia : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubscribeAsync<TEvent, THandler>(CancellationToken cancellationToken = default) where THandler : IIntegrationEventHandler<TEvent> => Task.CompletedTask;
}

public partial class Program { }
