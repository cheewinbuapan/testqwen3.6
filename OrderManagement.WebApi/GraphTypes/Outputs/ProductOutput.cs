namespace OrderManagement.WebApi.GraphTypes.Outputs;

/// <summary>
/// Product data exposed to clients.
/// </summary>
public class ProductOutput
{
    public string Id { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}