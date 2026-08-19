// Persistence surface for Acme.Orders.
//
// Microsoft.EntityFrameworkCore's DbContext and DbSet<T> are declared locally, like the other
// framework stand-ins in this fixture. This document derives `file_type: data-access` from the
// DbContext base type and carries the `persistence` tag from DbContext/DbSet.

using Acme.Shared.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>Stand-in for an EF Core entity set.</summary>
    public sealed class DbSet<TEntity> : List<TEntity>
        where TEntity : class;

    /// <summary>Stand-in for the EF Core unit of work.</summary>
    public abstract class DbContext
    {
        public int SaveChanges() => 0;
    }
}

namespace Acme.Orders.Data
{
    /// <summary>One order row as it is persisted.</summary>
    public sealed record OrderRecord(Guid OrderId, decimal Amount, OrderStatus Status);

    /// <summary>Unit of work for the orders schema.</summary>
    public sealed class OrderDbContext : DbContext
    {
        public DbSet<OrderRecord> Orders { get; } = [];
    }
}
