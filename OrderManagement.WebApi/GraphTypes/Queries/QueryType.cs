using HotChocolate;
using HotChocolate.Authorization;
using HotChocolate.Data;
using OrderManagement.WebApi.GraphTypes.Outputs;
using OrderManagement.WebApi.Models;

namespace OrderManagement.WebApi.GraphTypes.Queries;

/// <summary>
/// GraphQL Query root type.
/// Provides searchOrders for order search with filters and pagination.
/// </summary>
[QueryType]
public class QueryType
{
    /// <summary>
    /// Search orders with filters, sorting, and cursor pagination.
    /// </summary>
    [Authorize(Roles = new[] { "Admin" })]
    [GraphQLName("searchOrders")]
    [UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
    [UseFiltering]
    [UseSorting]
    public IQueryable<OrderSummary> GetSearchOrders(
        [Service] OrderService orderService,
        string? orderNumber = null,
        string? customerName = null,
        OrderStatus? status = null)
    {
        return orderService.GetOrdersQuery(orderNumber, customerName, status)
            .Select(order => new OrderSummary
            {
                OrderNumber = order.OrderNumber,
                CustomerName = order.CustomerName,
                Status = order.Status,
                TotalAmount = order.TotalAmount,
                ItemCount = order.Items.Count,
                CreatedAt = order.CreatedAt
            });
    }
}
