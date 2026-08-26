// Hand-written SQL beside the EF Core mapping.
//
// Six statement shapes the persistence classifier has to read differently: a readable SELECT with a
// WHERE, an INSERT with a column list, an UPDATE with a SET list, an EXEC naming a procedure, a
// DELETE whose bracketed target unquotes to a bare identifier, and an interpolated statement whose
// table is only known at run time and therefore stays unresolved. Each statement is executed at its
// own call site through OrderDbContext's raw-SQL surface, so its literal sits where the bounded SQL
// reader can read it - none of these strings are merely returned as text anymore.
//
// ConnectionString is this document's credential input. The SQL analyser walks every string
// expression here, so it sees this one too - and it must never reach a fact, a relation detail or a
// diagnostic. Its value is deliberately different from the one in appsettings.json, so the two
// absence checks cannot cover for each other. Neither is a real secret; nothing in this fixture
// opens a connection.

using Microsoft.EntityFrameworkCore;

namespace Acme.Orders.Data
{
    /// <summary>The service's hand-written SQL, executed through <see cref="OrderDbContext"/>'s raw-SQL surface.</summary>
    public sealed class OrderSqlQueries
    {
        private const string ConnectionString =
            "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=inline-fixture-secret;";

        private readonly OrderDbContext _context;

        public OrderSqlQueries(OrderDbContext context) => _context = context;

        public string Connection => ConnectionString;

        public DbSet<Order> SelectOrder() =>
            _context.Orders.FromSqlRaw("SELECT Id, Status FROM Orders WHERE Id = @id");

        public int InsertOrder() =>
            _context.ExecuteSqlRaw("INSERT INTO Orders (Id, Status, Amount) VALUES (@id, @status, @amount)");

        public int UpdateOrderStatus() =>
            _context.ExecuteSqlRaw("UPDATE Orders SET Status = @status WHERE Id = @id");

        public int RebuildTotals() => _context.ExecuteSqlRaw("EXEC usp_RebuildOrderTotals");

        /// <summary>The bracketed target unquotes to <c>Orders</c>, the same object <see cref="SelectOrder"/> reads.</summary>
        public int DeleteArchived() => _context.ExecuteSqlRaw("DELETE FROM [Orders] WHERE Status = 'Archived'");

        /// <summary>The table is a run-time value, so no node may be minted for it.</summary>
        public DbSet<Order> SelectAllFrom(string tableName) =>
            _context.Orders.FromSqlInterpolated($"SELECT * FROM {tableName}");
    }
}
