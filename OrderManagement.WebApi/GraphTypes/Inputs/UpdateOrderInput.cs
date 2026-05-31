namespace OrderManagement.WebApi.GraphTypes.Inputs;

public class UpdateOrderInput
{
    public List<Models.OrderItemInput> Items { get; set; } = new();
}
