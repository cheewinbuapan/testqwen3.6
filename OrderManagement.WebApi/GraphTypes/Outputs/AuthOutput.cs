using OrderManagement.WebApi.Models;

namespace OrderManagement.WebApi.GraphTypes.Outputs;

/// <summary>
/// Authentication response containing JWT token and user info.
/// Fields: token, user
/// </summary>
public class AuthOutput
{
    public string Token { get; set; } = string.Empty;
    public User User { get; set; } = new();
}

public class AuthOutputType : HotChocolate.Types.ObjectType<AuthOutput>
{
}
