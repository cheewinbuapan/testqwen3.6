using System.Security.Claims;
using HotChocolate;
using HotChocolate.Authorization;
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
public class MutationType
{
    /// <summary>
    /// Create a new user account.
    /// </summary>
    [Authorize(Roles = new[] { "Customer", "Admin" })]
    [GraphQLName("createUser")]
    public async Task<User> CreateUser(
        [Service] AuthService authService,
        CreateUserInput input)
    {
        if (input.Password != input.ConfirmPassword)
            throw new InvalidOperationException("Passwords do not match");

        return await authService.CreateUserAsync(
            input.Email,
            input.FirstName,
            input.LastName,
            input.Phone,
            input.Password,
            input.ConfirmPassword);
    }

    /// <summary>
    /// Login with email and password, returns JWT token.
    /// </summary>
    [GraphQLName("login")]
    public async Task<AuthOutput> Login(
        [Service] AuthService authService,
        string email,
        string password)
    {
        var result = await authService.LoginAsync(email, password);
        dynamic dyn = result;

        return new AuthOutput
        {
            Token = (string)dyn.Token,
            User = (User)dyn.User
        };
    }

    /// <summary>
    /// Create a new order.
    /// </summary>
    [Authorize(Roles = new[] { "Customer" })]
    [GraphQLName("createOrder")]
    public async Task<Order> CreateOrder(
        [Service] OrderService orderService,
        [Service] IHttpContextAccessor httpContextAccessor,
        CreateOrderInput input)
    {
        var customerId = GetCurrentCustomerId(httpContextAccessor, "You must be logged in to create an order");
        var result = await orderService.CreateOrderAsync(customerId, ToModelItems(input.Items));
        return (Order)result;
    }

    /// <summary>
    /// Update an existing order (Admin only).
    /// </summary>
    [Authorize(Roles = new[] { "Admin" })]
    [GraphQLName("updateOrder")]
    public async Task<Order> UpdateOrder(
        [Service] OrderService orderService,
        string id,
        UpdateOrderInput input)
    {
        var result = await orderService.UpdateOrderAsync(id, ToModelItems(input.Items));
        return (Order)result;
    }

    /// <summary>
    /// Confirm an order (Customer can only confirm own orders).
    /// </summary>
    [Authorize(Roles = new[] { "Customer" })]
    [GraphQLName("confirmOrder")]
    public async Task<Order> ConfirmOrder(
        [Service] OrderService orderService,
        [Service] IHttpContextAccessor httpContextAccessor,
        string id,
        string shippingAddress)
    {
        var customerId = GetCurrentCustomerId(httpContextAccessor, "You must be logged in to confirm an order");
        var result = await orderService.ConfirmOrderAsync(id, shippingAddress, customerId);
        return (Order)result;
    }

    /// <summary>
    /// Bulk update order status (Admin only).
    /// </summary>
    [Authorize(Roles = new[] { "Admin" })]
    [GraphQLName("bulkUpdateOrderStatus")]
    public async Task<BulkUpdateResult> BulkUpdateOrderStatus(
        [Service] OrderService orderService,
        List<string> ids,
        string status)
    {
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
    }

    private static string GetCurrentCustomerId(IHttpContextAccessor httpContextAccessor, string errorMessage)
    {
        var customerId = httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(customerId))
            throw new UnauthorizedAccessException(errorMessage);

        return customerId;
    }

    private static List<Models.OrderItemInput> ToModelItems(IEnumerable<Models.OrderItemInput> items)
    {
        return items
            .Select(item => new Models.OrderItemInput
            {
                ProductId = item.ProductId,
                Quantity = item.Quantity
            })
            .ToList();
    }
}
