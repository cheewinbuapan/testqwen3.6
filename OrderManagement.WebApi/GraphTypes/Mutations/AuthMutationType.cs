using HotChocolate.Types;
using OrderManagement.WebApi.GraphTypes.Inputs;
using OrderManagement.WebApi.GraphTypes.Outputs;
using OrderManagement.WebApi.Models;

namespace OrderManagement.WebApi.GraphTypes.Mutations;

/// <summary>
/// GraphQL Mutation root type.
/// Provides authentication mutations: createUser, login.
/// Provides order mutations: createOrder, updateOrder, confirmOrder.
/// Provides admin mutation: bulkUpdateOrderStatus.
/// </summary>
public class MutationType : ObjectType
{
    protected override void Configure(IObjectTypeDescriptor descriptor)
    {
        // Auth mutations
        descriptor.Field("createUser")
            .Description("Create a new user account.")
            .Authorize("Customer", "Admin")
            .Argument("input", a => a.Type(typeof(CreateUserInput)))
            .Type<UserType>()
            .Resolve(async context =>
            {
                var input = context.ArgumentValue<CreateUserInput>("input");
                var authService = context.Service<AuthService>();

                if (input.Password != input.ConfirmPassword)
                    throw new InvalidOperationException("Passwords do not match");

                return await authService.CreateUserAsync(
                    input.Email,
                    input.FirstName,
                    input.LastName,
                    input.Phone,
                    input.Password,
                    input.ConfirmPassword);
            });

        descriptor.Field("login")
            .Description("Login with email and password, returns JWT token.")
            .Argument("email", a => a.Type(typeof(string)))
            .Argument("password", a => a.Type(typeof(string)))
            .Type<AuthOutputType>()
            .Resolve(async context =>
            {
                var email = context.ArgumentValue<string>("email");
                var password = context.ArgumentValue<string>("password");
                var authService = context.Service<AuthService>();

                var result = await authService.LoginAsync(email, password);
                dynamic dyn = result;
                var token = (string)dyn.Token;
                var user = (User)dyn.User;
                return new AuthOutput { Token = token, User = user };
            });

        // Order mutations
        descriptor.Field("createOrder")
            .Description("Create a new order.")
            .Authorize("Customer")
            .Argument("input", a => a.Type(typeof(CreateOrderInput)))
            .Type<OrderType>()
            .Resolve(async context =>
            {
                var input = context.ArgumentValue<CreateOrderInput>("input");
                var orderService = context.Service<OrderService>();
                var httpContext = context.Service<IHttpContextAccessor>();

                var customerId = httpContext.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(customerId))
                    throw new UnauthorizedAccessException("You must be logged in to create an order");

                var modelItems = input.Items.Select(i => new Models.OrderItemInput
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList();

                var result = await orderService.CreateOrderAsync(customerId, modelItems);
                return (Order)result;
            });

        descriptor.Field("updateOrder")
            .Description("Update an existing order (Admin only).")
            .Authorize("Admin")
            .Argument("id", a => a.Type(typeof(string)))
            .Argument("input", a => a.Type(typeof(UpdateOrderInput)))
            .Type<OrderType>()
            .Resolve(async context =>
            {
                var id = context.ArgumentValue<string>("id");
                var input = context.ArgumentValue<UpdateOrderInput>("input");
                var orderService = context.Service<OrderService>();

                var modelItems = input.Items.Select(i => new Models.OrderItemInput
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity
                }).ToList();

                var result = await orderService.UpdateOrderAsync(id, modelItems);
                return (Order)result;
            });

        descriptor.Field("confirmOrder")
            .Description("Confirm an order (Customer can only confirm own orders).")
            .Authorize("Customer")
            .Argument("id", a => a.Type(typeof(string)))
            .Argument("shippingAddress", a => a.Type(typeof(string)))
            .Type<OrderType>()
            .Resolve(async context =>
            {
                var id = context.ArgumentValue<string>("id");
                var shippingAddress = context.ArgumentValue<string>("shippingAddress");
                var orderService = context.Service<OrderService>();
                var httpContext = context.Service<IHttpContextAccessor>();

                var customerId = httpContext.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(customerId))
                    throw new UnauthorizedAccessException("You must be logged in to confirm an order");

                var result = await orderService.ConfirmOrderAsync(id, shippingAddress, customerId);
                return (Order)result;
            });

        // Admin mutations
        descriptor.Field("bulkUpdateOrderStatus")
            .Description("Bulk update order status (Admin only).")
            .Authorize("Admin")
            .Argument("ids", a => a.Type(typeof(List<string>)))
            .Argument("status", a => a.Type(typeof(string)))
            .Type<BulkUpdateResultType>()
            .Resolve(async context =>
            {
                var ids = context.ArgumentValue<List<string>>("ids");
                var status = context.ArgumentValue<string>("status");
                var orderService = context.Service<OrderService>();

                var result = await orderService.BulkUpdateOrderStatusAsync(ids, status);
                dynamic dyn = result;

                return new BulkUpdateResult
                {
                    Succeeded = (int)dyn.Succeeded,
                    Failed = (int)dyn.Failed,
                    Results = ((IEnumerable<dynamic>)dyn.Results)
                        .Select(r => new ResultItem
                        {
                            Id = (string)r.Id,
                            OrderNumber = (string)r.OrderNumber,
                            PreviousStatus = (string)r.PreviousStatus,
                            NewStatus = (string)r.NewStatus,
                            Success = (bool)(r.Success ?? false),
                            ErrorMessage = r.ErrorMessage?.ToString()
                        })
                        .ToList()
                };
            });
    }
}
