using PacotePrivado.Broker;
using SistemaB.Composicao;

var builder = WebApplication.CreateBuilder(args);

// SCENARIO:HOST-002
builder.Services.AddSistemaB(builder.Configuration);

// Adapter de broker em memória para o host
builder.Services.AddSingleton<IEventBus, HostInMemoryEventBus>();

// SCENARIO:HOST-005
// Política CORS ampla em somente um sistema, marcada como cenário deliberado
builder.Services.AddCors(options =>
{
    options.AddPolicy("DeliberateWideCorsPolicy", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

builder.Services.AddControllers();

var app = builder.Build();

// SCENARIO:HOST-005
app.UseCors("DeliberateWideCorsPolicy");

app.MapControllers();

app.Run();

// Adapter mínimo para runtime do host
public class HostInMemoryEventBus : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task SubscribeAsync<TEvent, THandler>(CancellationToken cancellationToken = default) where THandler : IIntegrationEventHandler<TEvent> => Task.CompletedTask;
}

public partial class Program { }
