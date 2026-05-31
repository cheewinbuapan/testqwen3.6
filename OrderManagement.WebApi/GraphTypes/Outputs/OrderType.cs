using OrderManagement.WebApi.Models;

namespace OrderManagement.WebApi.GraphTypes.Outputs;

/// <summary>
/// GraphQL output type for Order entity.
/// Auto-maps: Id, OrderNumber, CustomerId, CustomerName, ShippingAddress, Status, TotalAmount, Items, CreatedAt, UpdatedAt
/// </summary>
public class OrderType : HotChocolate.Types.ObjectType<Order>
{
}
