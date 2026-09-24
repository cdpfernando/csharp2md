using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PacotePrivado.Broker;
using PacotePrivado.Parametros;
using SistemaB.Aplicacao;
using SistemaB.Dominio;
using SistemaB.Infraestrutura;
using SistemaB.Transversal;

namespace SistemaB.Composicao;

public static class SistemaBComposicaoExtensions
{
    // SCENARIO:HOST-002
    public static IServiceCollection AddSistemaB(this IServiceCollection services, IConfiguration configuration)
    {
        // SCENARIO:CFG-001
        // SCENARIO:DATA-003
        var connectionString = configuration.GetConnectionString("Primary") ?? "Data Source=synthetic_data.db";

        // SCENARIO:DATA-001
        services.AddDbContext<ContratacaoDbContext>(options => options.UseSqlite(connectionString));
        services.AddDbContext<OperacaoDbContext>(options => options.UseSqlite(connectionString));

        services.AddScoped<ContratacaoRepository>();
        services.AddScoped<OperacaoRepository>();

        // SCENARIO:PKG-001
        services.AddSingleton<IParameterProvider, DefaultParameterProvider>();

        // SCENARIO:PKG-002
        // SCENARIO:CFG-001
        services.Configure<BrokerOptions>(options =>
        {
            options.Brokers = configuration["Messaging:Brokers"] ?? "localhost:9092";
            options.Topic = configuration["Messaging:Topic"] ?? "pedidos-topic";
            options.SchemaRegistryUrl = configuration["Messaging:SchemaRegistryUrl"] ?? "http://localhost:8081";
            options.ClientPassword = configuration["Messaging:ClientPassword"] ?? "NOT_A_REAL_SECRET_ROTATE_ME";
        });

        // SCENARIO:HTTP-B-001
        services.AddHttpClient("ServicoCatalogo", client =>
        {
            client.BaseAddress = new Uri(configuration["Services:Catalog:BaseUrl"] ?? "http://localhost:5102");
            client.Timeout = TimeSpan.FromSeconds(5);
        });

        // SCENARIO:HTTP-B-002
        services.AddHttpClient("ServicoRisco", client =>
        {
            client.BaseAddress = new Uri(configuration["Services:Risk:BaseUrl"] ?? "http://localhost:5103");
            client.Timeout = TimeSpan.FromSeconds(3);
        });

        services.AddScoped<ServicoCatalogoClient>();
        services.AddScoped<ServicoRiscoClient>();
        services.AddScoped<IContratacaoGateway, ContratacaoGatewayAdapter>();

        services.AddScoped<ContratoCriadoLocalHandler>();
        services.AddScoped<ContratacaoUseCase>();

        // SCENARIO:HOST-003
        // Registro de decorator via scanning / reflection
        var decoratorType = typeof(LoggingContratacaoUseCaseDecorator);
        services.AddScoped<IContratacaoUseCase>(sp =>
        {
            var inner = sp.GetRequiredService<ContratacaoUseCase>();
            return (IContratacaoUseCase)Activator.CreateInstance(decoratorType, inner)!;
        });

        // SCENARIO:HOST-004
        // SCENARIO:NEG-008
        // Perfis genéricos carregados por reflexão (sem resolver serviços não registrados)
        var domainAssembly = typeof(IProfile<>).Assembly;
        var profileTypes = domainAssembly.GetTypes()
            .Where(t => !t.IsAbstract && !t.IsInterface && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProfile<>)));

        foreach (var pType in profileTypes)
        {
            var interfaceType = pType.GetInterfaces().First(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IProfile<>));
            services.AddSingleton(interfaceType, pType);
        }

        return services;
    }
}
