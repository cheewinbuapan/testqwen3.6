using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OrderManagement.WebApi.Models;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMongoDB(
        builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017",
        "OrderManagementDb"));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "OrderManagement",
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "OrderManagement",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"] ?? "secret"))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OrderService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapPost("/api/auth/register", async (AuthService auth, HttpContext context) =>
{
    var input = await context.Request.ReadFromJsonAsync<CreateUserRequest>();
    try
    {
        var user = await auth.CreateUserAsync(input.Email, input.FirstName, input.LastName, input.Phone, input.Password, input.ConfirmPassword);
        return Results.Ok(user);
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = "Invalid request" });
    }
});

app.MapPost("/api/auth/login", async (AuthService auth, HttpContext context) =>
{
    var input = await context.Request.ReadFromJsonAsync<LoginRequest>();
    try
    {
        return Results.Ok(await auth.LoginAsync(input.Email, input.Password));
    }
    catch
    {
        return Results.Unauthorized();
    }
});

app.MapPost("/api/orders", async (OrderService order, HttpContext context) =>
{
    var input = await context.Request.ReadFromJsonAsync<CreateOrderRequest>();
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    try
    {
        var items = input.Items.Select(i => new OrderItemInput { ProductId = i.ProductId, Quantity = i.Quantity }).ToList();
        return Results.Ok(await order.CreateOrderAsync(userId, items));
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = "Invalid request" });
    }
});

app.MapPut("/api/orders/{orderId}", async (OrderService order, string orderId, HttpContext context) =>
{
    var input = await context.Request.ReadFromJsonAsync<OrderItemsRequest>();
    try
    {
        var items = input.Items.Select(i => new OrderItemInput { ProductId = i.ProductId, Quantity = i.Quantity }).ToList();
        return Results.Ok(await order.UpdateOrderAsync(orderId, items));
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = "Invalid request" });
    }
});

app.MapPost("/api/orders/{orderId}/confirm", async (OrderService order, string orderId, HttpContext context) =>
{
    var input = await context.Request.ReadFromJsonAsync<ConfirmOrderRequest>();
    var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    try
    {
        return Results.Ok(await order.ConfirmOrderAsync(orderId, input.ShippingAddress, userId));
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = "Invalid request" });
    }
});

app.MapGet("/api/orders", async (OrderService order, [FromQuery] string orderNumber, [FromQuery] string customerName, [FromQuery] string status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20) =>
{
    try
    {
        return Results.Ok(await order.SearchOrdersAsync(
            string.IsNullOrEmpty(orderNumber) ? null : orderNumber,
            string.IsNullOrEmpty(customerName) ? null : customerName,
            string.IsNullOrEmpty(status) ? null : status,
            page, pageSize));
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = "Invalid request" });
    }
});

app.MapPost("/api/admin/bulk-update", async (OrderService order, HttpContext context) =>
{
    var input = await context.Request.ReadFromJsonAsync<BulkUpdateRequest>();
    try
    {
        return Results.Ok(await order.BulkUpdateOrderStatusAsync(input.Ids.ToList(), input.Status));
    }
    catch (Exception)
    {
        return Results.BadRequest(new { error = "Invalid request" });
    }
});

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await SeedData.SeedAsync(context);
}

app.Run();

public record CreateUserRequest(string Email, string FirstName, string LastName, string Phone, string Password, string ConfirmPassword);
public record LoginRequest(string Email, string Password);
public record CreateOrderRequest(OrderItemInput[] Items);
public record ConfirmOrderRequest(string ShippingAddress);
public record BulkUpdateRequest(string[] Ids, string Status);
public record OrderItemsRequest(OrderItemInput[] Items);
