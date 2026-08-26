// Persistence surface for Acme.Orders.
//
// Microsoft.EntityFrameworkCore's DbContext and DbSet<T> are declared locally, like the other
// framework stand-ins in this fixture. This document derives `file_type: data-access` from the
// DbContext base type and carries the `persistence` tag from DbContext/DbSet.
//
// The query and write services live beside the context on purpose: the data-access claim pass
// catalogues DbSet properties per document, so a query whose context is declared elsewhere is not
// recognised. The table and column configuration deliberately lives in another document
// (OrderConfiguration.cs), which is what exercises the cross-document mapping pass.
//
// DbSet<T> and DbContext also expose EF Core's raw-SQL surface (FromSqlRaw, FromSqlInterpolated,
// ExecuteSqlRaw), so OrderSqlQueries.cs can execute its hand-written statements instead of merely
// returning them as text.

using Acme.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>Stand-in for an EF Core entity set.</summary>
    public sealed class DbSet<TEntity> : List<TEntity>
        where TEntity : class
    {
        public DbSet<TEntity> FromSqlRaw(string sql) => this;

        public DbSet<TEntity> FromSqlInterpolated(FormattableString sql) => this;
    }

    /// <summary>Stand-in for the EF Core unit of work.</summary>
    public abstract class DbContext
    {
        public int SaveChanges() => 0;

        public int ExecuteSqlRaw(string sql) => 0;
    }
}

namespace Acme.Orders.Data
{
    /// <summary>One order row as it is persisted. Its table and Status column are configured.</summary>
    public sealed class Order
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }

        public OrderStatus Status { get; set; }
    }

    /// <summary>One order line. Deliberately left unconfigured, so its table stays a convention.</summary>
    public sealed class OrderLine
    {
        public Guid Id { get; set; }

        public decimal Amount { get; set; }

        public string Sku { get; set; } = string.Empty;
    }

    /// <summary>Unit of work for the orders schema.</summary>
    public sealed class OrderDbContext : DbContext
    {
        public DbSet<Order> Orders { get; } = [];

        public DbSet<OrderLine> OrderLines { get; } = [];
    }

    /// <summary>Reads over the orders schema: one filtered column, three projected ones.</summary>
    public sealed class OrderQueries
    {
        private readonly OrderDbContext _context;

        public OrderQueries(OrderDbContext context) => _context = context;

        public object? GetOrder(Guid orderId) =>
            _context.Orders
                .Where(order => order.Id == orderId)
                .Select(order => new { order.Id, order.Status, order.Amount })
                .FirstOrDefault();
    }

    /// <summary>Writes over the orders schema, both tracked and through the entity set.</summary>
    public sealed class OrderWrites
    {
        private readonly OrderDbContext _context;

        public OrderWrites(OrderDbContext context) => _context = context;

        /// <summary>A tracked write: the assigned property name matches exactly one exposed entity.</summary>
        public void PayOrder(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            order.Status = OrderStatus.Authorized;
            _context.SaveChanges();
        }

        /// <summary>A tracked write whose property name matches two exposed entities.</summary>
        public void Reprice(Order order, decimal amount)
        {
            ArgumentNullException.ThrowIfNull(order);

            order.Amount = amount;
            _context.SaveChanges();
        }

        /// <summary>An insert through the entity set itself.</summary>
        public void PlaceOrder(Order order)
        {
            _context.Orders.Add(order);
            _context.SaveChanges();
        }
    }
}
