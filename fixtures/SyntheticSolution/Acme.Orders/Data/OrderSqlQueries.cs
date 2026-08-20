// Hand-written SQL beside the EF Core mapping.
//
// Six statement shapes the discovery stage has to read differently: a readable SELECT with a WHERE,
// an INSERT with a column list, an UPDATE with a SET list, an EXEC naming a procedure, a DELETE
// whose bracketed target the bounded reader refuses to read, and an interpolated statement whose
// table is only known at run time.
//
// ConnectionString is this document's credential input. The SQL analyser walks every string
// expression here, so it sees this one too - and it must never reach a fact, a relation detail or a
// diagnostic. Its value is deliberately different from the one in appsettings.json, so the two
// absence checks cannot cover for each other. Neither is a real secret; nothing in this fixture
// opens a connection.

namespace Acme.Orders.Data
{
    /// <summary>The service's hand-written SQL, as text. Nothing here executes.</summary>
    public sealed class OrderSqlQueries
    {
        private const string ConnectionString =
            "Server=orders-db.internal.acme.local;Database=Orders;User Id=orders_app;Password=inline-fixture-secret;";

        public string Connection => ConnectionString;

        public string SelectOrder() => "SELECT Id, Status FROM Orders WHERE Id = @id";

        public string InsertOrder() => "INSERT INTO Orders (Id, Status, Amount) VALUES (@id, @status, @amount)";

        public string UpdateOrderStatus() => "UPDATE Orders SET Status = @status WHERE Id = @id";

        public string RebuildTotals() => "EXEC usp_RebuildOrderTotals";

        /// <summary>A bracketed target the bounded reader will not read, so the access stays unresolved.</summary>
        public string DeleteArchived() => "DELETE FROM [Orders] WHERE Status = 'Archived'";

        /// <summary>The table is a run-time value, so no node may be minted for it.</summary>
        public string SelectAllFrom(string tableName) => $"SELECT * FROM {tableName}";
    }
}
