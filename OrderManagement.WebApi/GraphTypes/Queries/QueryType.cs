using HotChocolate.Types;
using OrderManagement.WebApi.GraphTypes.Inputs;
using OrderManagement.WebApi.GraphTypes.Outputs;
using OrderManagement.WebApi.Models;

namespace OrderManagement.WebApi.GraphTypes.Queries;

/// <summary>
/// GraphQL Query root type.
/// Provides searchOrders for order search with filters and pagination.
/// </summary>
public class QueryType : ObjectType
{
    protected override void Configure(IObjectTypeDescriptor descriptor)
    {
        descriptor.Field("searchOrders")
            .Description("Search orders with filters and pagination.")
            .Authorize("Admin")
            .Type<OrderSummaryType>()
            .Argument("orderNumber", a => a.Type(typeof(string)))
            .Argument("customerName", a => a.Type(typeof(string)))
            .Argument("status", a => a.Type(typeof(OrderStatus)))
            .Argument("page", a => a.Type(typeof(int)).DefaultValue(1))
            .Argument("pageSize", a => a.Type(typeof(int)).DefaultValue(20))
            .Resolve(async context =>
            {
                var orderService = context.Service<OrderService>();
                var orderNumber = context.ArgumentValue<string?>("orderNumber");
                var customerName = context.ArgumentValue<string?>("customerName");
                var status = context.ArgumentValue<OrderStatus?>("status");
                var page = context.ArgumentValue<int>("page");
                var pageSize = context.ArgumentValue<int>("pageSize");

                return await orderService.SearchOrdersAsync(
                    orderNumber,
                    customerName,
                    status?.ToString(),
                    page,
                    pageSize);
            });
    }
}
