namespace OrderManagement.WebApi.GraphTypes.Outputs;

/// <summary>
/// Result of a bulk order status update operation.
/// Fields: succeeded, failed, results
/// </summary>
public class BulkUpdateResult
{
    public int Succeeded { get; set; }
    public int Failed { get; set; }
    public List<ResultItem> Results { get; set; } = new();
}

public class BulkUpdateResultType : HotChocolate.Types.ObjectType<BulkUpdateResult>
{
}

/// <summary>
/// Individual result from a bulk update operation.
/// Fields: id, orderNumber, previousStatus, newStatus, success, errorMessage
/// </summary>
public class ResultItem
{
    public string Id { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public string PreviousStatus { get; set; } = string.Empty;
    public string NewStatus { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ResultItemType : HotChocolate.Types.ObjectType<ResultItem>
{
}
