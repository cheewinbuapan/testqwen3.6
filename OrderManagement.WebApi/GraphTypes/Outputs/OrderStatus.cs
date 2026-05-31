using OrderManagement.WebApi.Models;

namespace OrderManagement.WebApi.GraphTypes.Outputs;

/// <summary>
/// GraphQL enum type for Order status values.
/// Maps to: Pending, Confirmed
/// </summary>
public class OrderStatusType : HotChocolate.Types.EnumType<OrderStatus>
{
}
