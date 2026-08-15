// Dependency-injection registration surface for Acme.Orders.
//
// Stand-in for Microsoft.Extensions.DependencyInjection, declared locally so the fixture keeps
// building without the real package. The registration methods are genuine extension methods on a
// static class, which is what makes this document derive `file_type: extension` rather than
// `class`, and what puts `dependency-injection` in its tags.

namespace Acme.Orders.Hosting;

/// <summary>Stand-in for <c>IServiceCollection</c>.</summary>
public interface IServiceCollection
{
    IReadOnlyCollection<Type> Registrations { get; }

    void Add(Type serviceType);
}

/// <summary>Stand-in for the default <c>IServiceCollection</c> implementation.</summary>
public sealed class ServiceCollection : IServiceCollection
{
    private readonly List<Type> _registrations = [];

    public IReadOnlyCollection<Type> Registrations => _registrations;

    public void Add(Type serviceType) => _registrations.Add(serviceType);
}

/// <summary>The lifetime-scoped registration helpers every ASP.NET Core composition root uses.</summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddScoped<TService>(this IServiceCollection services)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(typeof(TService));

        return services;
    }

    public static IServiceCollection AddSingleton<TService>(this IServiceCollection services)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(typeof(TService));

        return services;
    }

    public static IServiceCollection AddTransient<TService>(this IServiceCollection services)
        where TService : class
    {
        ArgumentNullException.ThrowIfNull(services);
        services.Add(typeof(TService));

        return services;
    }
}
