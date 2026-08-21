// A repository in name only.
//
// Nothing here touches a database: no DbContext, no DbSet, no SQL, no command. The name, the folder
// and the namespace are the only persistence-shaped things about it, and none of them is evidence.
// This document exists so the discovery stage can be shown to emit nothing for it.

namespace Acme.Orders.Data
{
    /// <summary>Holds orders in memory. Named like a repository, backed by nothing.</summary>
    public sealed class OrderRepository
    {
        private readonly Dictionary<Guid, Order> _byId = [];

        public Order? Find(Guid orderId) => _byId.GetValueOrDefault(orderId);

        public void Save(Order order)
        {
            ArgumentNullException.ThrowIfNull(order);

            _byId[order.Id] = order;
        }
    }
}
