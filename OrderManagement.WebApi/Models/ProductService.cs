namespace OrderManagement.WebApi.Models;

public class ProductService
{
    private readonly ApplicationDbContext _context;
    public ProductService(ApplicationDbContext context) { _context = context; }

    public IQueryable<Product> GetProductsQuery()
    {
        return _context.Products.AsQueryable();
    }
}
