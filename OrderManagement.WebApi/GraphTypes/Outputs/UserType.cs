using OrderManagement.WebApi.Models;

namespace OrderManagement.WebApi.GraphTypes.Outputs;

/// <summary>
/// GraphQL output type for User entity.
/// Auto-maps: Id, Email, FirstName, LastName, Phone, Role, CreatedAt, UpdatedAt
/// </summary>
public class UserType : HotChocolate.Types.ObjectType<User>
{
}
