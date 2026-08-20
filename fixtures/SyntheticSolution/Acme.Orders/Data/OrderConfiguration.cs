// Entity configuration for Acme.Orders.
//
// EF Core's ModelBuilder, EntityTypeBuilder<T> and PropertyBuilder are declared locally, like the
// DbContext/DbSet stand-ins beside them. This document sits apart from the entity it configures on
// purpose: proving Order's table needs the whole run in view, not one document.

using Microsoft.EntityFrameworkCore;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>Stand-in for the fluent column builder.</summary>
    public sealed class PropertyBuilder
    {
        public PropertyBuilder HasColumnName(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return this;
        }
    }

    /// <summary>Stand-in for the fluent entity builder.</summary>
    public sealed class EntityTypeBuilder<TEntity>
        where TEntity : class
    {
        public EntityTypeBuilder<TEntity> ToTable(string name)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);

            return this;
        }

        public PropertyBuilder Property<TProperty>(Func<TEntity, TProperty> selector)
        {
            ArgumentNullException.ThrowIfNull(selector);

            return new PropertyBuilder();
        }
    }

    /// <summary>Stand-in for the model builder handed to <c>OnModelCreating</c>.</summary>
    public sealed class ModelBuilder
    {
        public EntityTypeBuilder<TEntity> Entity<TEntity>()
            where TEntity : class => new();
    }
}

namespace Acme.Orders.Data
{
    /// <summary>Maps <see cref="Order"/> onto its table and names one of its columns.</summary>
    public sealed class OrderConfiguration
    {
        public void Configure(ModelBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);

            builder.Entity<Order>().ToTable("order_headers");
            builder.Entity<Order>().Property(order => order.Status).HasColumnName("order_status");
        }
    }
}
