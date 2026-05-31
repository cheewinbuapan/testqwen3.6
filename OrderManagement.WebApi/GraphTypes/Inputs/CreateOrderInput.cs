namespace OrderManagement.WebApi.GraphTypes.Inputs;

public class CreateOrderInput
{
    public string CustomerId { get; set; } = string.Empty;
    public List<Models.OrderItemInput> Items { get; set; } = new();
}
