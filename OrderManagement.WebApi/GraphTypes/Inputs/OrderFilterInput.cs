namespace OrderManagement.WebApi.GraphTypes.Inputs;

public class OrderFilterInput
{
    public string? OrderNumber { get; set; }
    public string? CustomerName { get; set; }
    public Models.OrderStatus? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
