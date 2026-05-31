using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace OrderManagement.WebApi.Models;

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
