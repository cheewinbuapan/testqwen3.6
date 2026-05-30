using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;

namespace OrderManagement.WebApi.Models;

public class AuthService
{
    private readonly ApplicationDbContext _context;
    private readonly IConfiguration _config;

    public AuthService(ApplicationDbContext context, IConfiguration config)
    {
        _context = context;
        _config = config;
    }

    public async Task<User> CreateUserAsync(string email, string firstName, string lastName, string phone, string password, string confirmPassword)
    {
        if (password != confirmPassword) throw new InvalidOperationException("Passwords do not match");
        if (await _context.Users.AnyAsync(u => u.Email == email)) throw new InvalidOperationException("Email already exists");

        var user = new User
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password)
        };
        _context.Users.Add(user);
        await _context.SaveChangesAsync();
        return user;
    }

    public async Task<object> LoginAsync(string email, string password)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
        if (user == null || !BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password");

        var secretKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["JwtSettings:SecretKey"] ?? "secret"));
        var creds = new SigningCredentials(secretKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _config["JwtSettings:Issuer"],
            audience: _config["JwtSettings:Audience"],
            claims: new[] {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.Role)
            },
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds);

        return new { Token = new JwtSecurityTokenHandler().WriteToken(token), User = user };
    }
}

public class OrderService
{
    private readonly ApplicationDbContext _context;
    public OrderService(ApplicationDbContext context) { _context = context; }

    public async Task<object> CreateOrderAsync(string customerId, List<OrderItemInput> items)
    {
        var customer = await _context.Users.FindAsync(customerId);
        if (customer == null) throw new InvalidOperationException("Customer not found");
        if (items == null || !items.Any()) throw new InvalidOperationException("Order must have at least one item");

        var cust = (User)customer;
        var orderItems = new List<OrderItem>();
        decimal total = 0;

        foreach (var item in items)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null) throw new InvalidOperationException($"Product not found: {item.ProductId}");
            if (product.Stock < item.Quantity) throw new InvalidOperationException("Insufficient stock");

            orderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNumber = product.ProductNumber,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
            total += orderItems.Last().SubTotal;
        }

        var orderNum = GenerateOrderNumber();
        var order = new Order
        {
            OrderNumber = orderNum,
            CustomerId = customerId,
            CustomerName = $"{cust.FirstName} {cust.LastName}",
            Status = OrderStatus.Pending,
            TotalAmount = total,
            Items = orderItems
        };
        _context.Orders.Add(order);
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<object> UpdateOrderAsync(string orderId, List<OrderItemInput> items)
    {
        var order = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) throw new InvalidOperationException("Order not found");
        if (order.Status != OrderStatus.Pending) throw new InvalidOperationException("Only Pending orders can be updated");
        if (items == null || !items.Any()) throw new InvalidOperationException("Order must have at least one item");

        var newItems = new List<OrderItem>();
        decimal total = 0;
        foreach (var item in items)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null) throw new InvalidOperationException($"Product not found: {item.ProductId}");
            newItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNumber = product.ProductNumber,
                ProductName = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price
            });
            total += newItems.Last().SubTotal;
        }
        order.Items.Clear();
        order.Items.AddRange(newItems);
        order.TotalAmount = total;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<object> ConfirmOrderAsync(string orderId, string shippingAddress, string customerId)
    {
        var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
        if (order == null) throw new InvalidOperationException("Order not found");
        if (order.Status != OrderStatus.Pending) throw new InvalidOperationException("Not in Pending status");
        if (order.CustomerId != customerId) throw new InvalidOperationException("You can only confirm your own orders");
        if (string.IsNullOrWhiteSpace(shippingAddress)) throw new InvalidOperationException("Shipping address required");

        order.ShippingAddress = shippingAddress;
        order.Status = OrderStatus.Confirmed;
        order.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return order;
    }

    public async Task<object> SearchOrdersAsync(string? orderNumber = null, string? customerName = null, string? status = null, int page = 1, int pageSize = 20)
    {
        var query = _context.Orders.AsQueryable();
        if (!string.IsNullOrEmpty(orderNumber))
            query = query.Where(o => o.OrderNumber.Contains(orderNumber));
        if (!string.IsNullOrEmpty(customerName))
            query = query.Where(o => o.CustomerName.Contains(customerName));
        if (status == "PENDING")
            query = query.Where(o => o.Status == OrderStatus.Pending);
        if (status == "CONFIRMED")
            query = query.Where(o => o.Status == OrderStatus.Confirmed);

        var total = await query.CountAsync();
        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(o => new { o.Id, o.OrderNumber, o.CustomerName, o.Status, o.TotalAmount, ItemCount = o.Items.Count, o.CreatedAt })
            .ToListAsync();
        return new { TotalCount = total, Orders = orders };
    }

    public async Task<object> BulkUpdateOrderStatusAsync(List<string> ids, string status)
    {
        var results = new List<object>();
        foreach (var id in ids)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
            {
                results.Add(new { Id = id, OrderNumber = "", PreviousStatus = "", NewStatus = status, Success = false, ErrorMessage = "Order not found" });
                continue;
            }
            if (order.Status != OrderStatus.Pending)
            {
                results.Add(new { Id = id, OrderNumber = order.OrderNumber, PreviousStatus = order.Status.ToString(), NewStatus = status, Success = false, ErrorMessage = "Not in Pending status" });
                continue;
            }
            order.Status = status == "CONFIRMED" ? OrderStatus.Confirmed : OrderStatus.Pending;
            order.UpdatedAt = DateTime.UtcNow;
            results.Add(new { Id = id, OrderNumber = order.OrderNumber, PreviousStatus = "Pending", NewStatus = status, Success = true });
            await _context.SaveChangesAsync();
        }
        return new
        {
            Succeeded = results.Count(r => (r as dynamic).Success),
            Failed = results.Count(r => !(r as dynamic).Success),
            Results = results
        };
    }

    private string GenerateOrderNumber()
    {
        var dateStr = DateTime.UtcNow.ToString("yyyyMMdd");
        var count = _context.Orders.CountAsync(o => o.OrderNumber.StartsWith($"ORD-{dateStr}")).Result;
        return $"ORD-{dateStr}-{count + 1:D4}";
    }
}
