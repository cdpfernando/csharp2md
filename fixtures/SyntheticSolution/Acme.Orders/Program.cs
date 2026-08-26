// Host bootstrap for Acme.Orders, shaped like a minimal ASP.NET Core entry point.
//
// Main exists so the project can compile as an application (OutputType=Exe) without changing
// the observable composition-root surface: it delegates to ConfigureHost and adds no new
// configuration key, client name, or persistence call.
//
// The fixture declares the Microsoft.AspNetCore.Builder types itself rather than referencing the
// ASP.NET Core framework, for the same reason PaymentsGrpcClient.cs declares Grpc.Core.ClientBase:
// the fixture must build and restore without pulling in an external dependency. The frontmatter
// heuristics classify this file as `configuration` from its name and tag it `bootstrapping` from
// the WebApplication.CreateBuilder call, so the stand-in exercises the real rules.
//
// ConfigureHost also binds OrderDbContext to a configuration key: it calls
// Configuration.GetConnectionString("OrdersDb") in the same callable that registers the context, and
// the context type appears in this callable's signature so the ledger can bind the key (PK-11).

using Acme.Orders.Hosting;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace Microsoft.AspNetCore.Builder
{
    /// <summary>Stand-in for the host builder returned by <c>WebApplication.CreateBuilder</c>.</summary>
    public sealed class WebApplicationBuilder
    {
        public IServiceCollection Services { get; } = new ServiceCollection();

        public IConfiguration Configuration { get; } = new ConfigurationRoot();
    }

    /// <summary>Stand-in for the ASP.NET Core application entry type.</summary>
    public static class WebApplication
    {
        public static WebApplicationBuilder CreateBuilder(string[] args)
        {
            ArgumentNullException.ThrowIfNull(args);

            return new WebApplicationBuilder();
        }
    }
}

namespace Acme.Orders
{
    /// <summary>Composition root: builds the host and registers the service's own dependencies.</summary>
    public static class Program
    {
        public static void Main(string[] args)
        {
            _ = ConfigureHost(args);
        }

        public static WebApplicationBuilder ConfigureHost(string[] args, Data.OrderDbContext? context = null)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddScoped<OrderService>();
            builder.Services.AddSingleton<Data.OrderDbContext>();
            _ = builder.Configuration.GetConnectionString("OrdersDb");
            _ = context;

            return builder;
        }
    }
}
