namespace OrderManagement.WebApi.Models;

public static class SeedData
{
    public static async Task SeedAsync(ApplicationDbContext context)
    {
        if (context.ProductStatuses.Any()) return;

        var statuses = new List<ProductStatus>
        {
            new() { Id = "PS001", Code = "PENDING", DisplayName = "รอยืนยันคำสั่งซื้อ" },
            new() { Id = "PS002", Code = "CONFIRMED", DisplayName = "ยืนยันคำสั่งซื้อ" }
        };
        context.ProductStatuses.AddRange(statuses);
        await context.SaveChangesAsync();

        var products = new List<Product>
        {
            new() { Id = "P001", ProductNumber = "PROD-001", Name = "สินค้าตัวอย่าง 1", Price = 299.00m, Stock = 100 },
            new() { Id = "P002", ProductNumber = "PROD-002", Name = "สินค้าตัวอย่าง 2", Price = 590.00m, Stock = 50 },
            new() { Id = "P003", ProductNumber = "PROD-003", Name = "สินค้าตัวอย่าง 3", Price = 1250.00m, Stock = 30 }
        };
        context.Products.AddRange(products);
        await context.SaveChangesAsync();
    }
}
