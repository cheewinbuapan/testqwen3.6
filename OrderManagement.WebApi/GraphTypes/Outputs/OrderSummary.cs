namespace OrderManagement.WebApi.GraphTypes.Outputs;

/// <summary>
/// Summary view of an Order for search results.
/// Fields: orderNumber, customerName, status, totalAmount, itemCount, createdAt
/// </summary>
public class OrderSummary
{
    public string OrderNumber { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public Models.OrderStatus Status { get; set; }
    public decimal TotalAmount { get; set; }
    public int ItemCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class OrderSummaryType : HotChocolate.Types.ObjectType<OrderSummary>
{
}
