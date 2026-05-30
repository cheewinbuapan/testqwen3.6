# Migration Plan: REST to GraphQL with HotChocolate 15.1.12

## Overview

 migrating from REST API (ASP.NET Minimal API) to GraphQL API using HotChocolate v15.1.12 with EF Core MongoDB provider.

**Current State:** REST API using `MapPost`/`MapGet`/`MapPut` — 7 endpoints
**Target State:** GraphQL API at `/graphql` — HotChocolate 15.1.12, code-first approach

---

## Step 1: Add HotChocolate NuGet Packages

**File:** `OrderManagement.WebApi/OrderManagement.WebApi.csproj`

Add the following `PackageReference` elements inside the existing `<ItemGroup>`:

```xml
<PackageReference Include="HotChocolate.AspNetCore" Version="15.1.12" />
<PackageReference Include="HotChocolate.Data" Version="15.1.12" />
<PackageReference Include="HotChocolate.Authorization" Version="15.1.12" />
<PackageReference Include="HotChocolate.Types.OffsetPagination" Version="15.1.12" />
```

**All 4 packages target .NET 8.0 and are available at version 15.1.12 on NuGet.**

---

## Step 2: Create GraphQL Schema Files

### 2.1 Directory Structure

Create the following directory structure under `OrderManagement.WebApi/`:

```
OrderManagement.WebApi/
├── GraphTypes/
│   ├── Inputs/                          # GraphQL InputObject types
│   │   ├── CreateUserInput.cs
│   │   ├── LoginInput.cs
│   │   ├── CreateOrderInput.cs
│   │   ├── UpdateOrderInput.cs
│   │   ├── ConfirmOrderInput.cs
│   │   ├── OrderFilterInput.cs
│   │   └── OrderItemInput.cs
│   ├── Outputs/                         # GraphQL ObjectType/OutputType
│   │   ├── UserType.cs
│   │   ├── OrderType.cs
│   │   ├── OrderSummaryType.cs
│   │   ├── AuthOutputType.cs
│   │   ├── BulkUpdateResultType.cs
│   │   └── OrderStatusType.cs
│   ├── Queries/
│   │   └── QueryType.cs               # Root Query type
│   └── Mutations/
│       ├── MutationType.cs              # Root Mutation type
│       ├── AuthMutationType.cs
│       ├── OrderMutationType.cs
│       └── AdminMutationType.cs
├── Models/                              # (existing)
│   ├── Entities.cs
│   ├── DataContext.cs
│   ├── Services.cs
│   └── SeedData.cs
├── Program.cs                           # (modify)
└── appsettings.json                     # (existing)
```

### 2.2 Input Types (Code-first)

Each uses HotChocolate's `InputType<T>` class:

| File | HotChocolate Class | Maps To |
|------|-------------------|---------|
| `CreateUserInput.cs` | `InputType<CreateUserInput>` | Email, FirstName, LastName, Phone, Password, ConfirmPassword |
| `LoginInput.cs` | `InputType<LoginInput>` | Email, Password |
| `CreateOrderInput.cs` | `InputType<CreateOrderInput>` | CustomerId, Items (OrderItemInput[]) |
| `UpdateOrderInput.cs` | `InputType<UpdateOrderInput>` | Items (OrderItemInput[]) |
| `ConfirmOrderInput.cs` | `InputType<ConfirmOrderInput>` | ShippingAddress |
| `OrderFilterInput.cs` | `InputType<OrderFilterInput>` | OrderNumber, CustomerName, Status, Page, PageSize |
| `OrderItemInput.cs` | `InputType<OrderItemInput>` | ProductId, Quantity |

**Note:** The existing `OrderItemInput` class in `Entities.cs` can be reused or moved to a shared DTO folder.

### 2.3 Output Types

Each uses HotChocolate's `ObjectType<T>` class:

| File | HotChocolate Class | Maps To |
|------|-------------------|---------|
| `UserType.cs` | `ObjectType<User>` | Id, Email, FirstName, LastName, Phone, Role, CreatedAt |
| `OrderType.cs` | `ObjectType<Order>` | Id, OrderNumber, CustomerName, Status, TotalAmount, Items, ShippingAddress, CreatedAt |
| `OrderSummaryType.cs` | `ObjectType<OrderSummary>` | orderNumber, customerName, status, totalAmount, itemCount, createdAt |
| `AuthOutputType.cs` | `ObjectType<AuthOutput>` | token, user |
| `BulkUpdateResultType.cs` | `ObjectType<BulkUpdateResult>` | succeeded, failed, results |
| `BulkUpdateResultItem.cs` | `ObjectType<ResultItem>` | id, orderNumber, previousStatus, newStatus, success, errorMessage |
| `OrderStatusType.cs` | Enum Type | Pending, Confirmed |

---

## Step 3: Create GraphQL Resolvers

### 3.1 Query Type (`GraphTypes/Queries/QueryType.cs`)

```csharp
[TypeMapping(typeof(OrderService))]
public class QueryType
{
    public enum OrderFilter { OrderNumber, CustomerName, Status }

    [UseFiltering]
    [UsePagination]
    public async Task<PagedResults<Order>> SearchOrdersAsync(
        [Service] OrderService service,
        [DefaultValue(1)] int page,
        [DefaultValue(20)] int pageSize,
        string? orderNumber,
        string? customerName,
        OrderStatus? status)
    {
        return await service.SearchOrdersAsync(orderNumber, customerName, status, page, pageSize);
    }
}
```

### 3.2 Mutation Types

**Auth Mutation** (`GraphTypes/Mutations/AuthMutationType.cs`):

```csharp
public class AuthMutationType
{
    [UseAuthorize(Roles = "Customer")]
    public async Task<User> CreateUserAsync(
        [Service] AuthService auth,
        CreateUserInput input)
    {
        // Validate ConfirmPassword == Password
        // Call auth.CreateUserAsync(...)
    }

    public async Task<AuthOutput> LoginAsync(
        [Service] AuthService auth,
        LoginInput input)
    {
        // Call auth.LoginAsync(...)
        // Return AuthOutput { token, user }
    }
}
```

**Order Mutation** (`GraphTypes/Mutations/OrderMutationType.cs`):

```csharp
[UseAuthorize]
public class OrderMutationType
{
    public async Task<Order> CreateOrderAsync(
        [Service] OrderService order,
        CreateOrderInput input,
        IHttpContextAccessor http)
    {
        string userId = http.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return await order.CreateOrderAsync(userId, input.Items);
    }

    [UseAuthorize(Roles = "Admin")]
    public async Task<Order> UpdateOrderAsync(
        [Service] OrderService order,
        UpdateOrderInput input,
        string id)
    {
        return await order.UpdateOrderAsync(id, input.Items);
    }

    public async Task<Order> ConfirmOrderAsync(
        [Service] OrderService order,
        ConfirmOrderInput input,
        string id,
        IHttpContextAccessor http)
    {
        string userId = http.HttpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return await order.ConfirmOrderAsync(id, input.ShippingAddress, userId);
    }
}
```

**Admin Mutation** (`GraphTypes/Mutations/AdminMutationType.cs`):

```csharp
[UseAuthorize(Roles = "Admin")]
public class AdminMutationType
{
    public async Task<BulkUpdateResult> BulkUpdateOrderStatusAsync(
        [Service] OrderService order,
        BulkUpdateInput input)
    {
        return await order.BulkUpdateOrderStatusAsync(input.Ids, input.Status);
    }
}
```

---

## Step 4: Configure Program.cs

### 4.1 Add GraphQL Middleware

Replace the existing `app.Run()` and REST endpoint registrations with:

```csharp
builder.Services.AddGraphQLServer()
    .AddAuthorizeDirective()
    .AddType<QueryType>()
    .AddType<MutationType>()
    .AddType<OrderType>()
    .AddType<UserType>()
    // ... add all other types
    .AddMetrics()
    .AddQueryField(q => q
        .Name("searchOrders")
        .Description("Search orders with filters")
        .Type<OrderSummaryType>()
        .Argument("filter", "Order filter input")
        .Authorize("Admin")
        .BindResolver<QueryResolver>());

app.UseGraphQLPlayground();
app.UseGraphQL<QueryResolver>();
```

### 4.2 Keep Existing Infrastructure

- JWT Bearer authentication: **unchanged**
- EF Core MongoDB DbContext: **unchanged**
- AuthService, OrderService: **unchanged** (same business logic)
- SeedData.SeedAsync: **unchanged** (still runs on startup)
- appsettings.json: **unchanged**

### 4.3 Remove REST Endpoints

Delete these 7 Map* lines from `Program.cs`:

| Remove | Endpoint |
|--------|----------|
| `app.MapPost("/api/auth/register", ...)` | |
| `app.MapPost("/api/auth/login", ...)` | |
| `app.MapPost("/api/orders", ...)` | |
| `app.MapPut("/api/orders/{orderId}", ...)` | |
| `app.MapPost("/api/orders/{orderId}/confirm", ...)` | |
| `app.MapGet("/api/orders", ...)` | |
| `app.MapPost("/api/admin/bulk-update", ...)` | |

Replace with GraphQL schema registration (see Step 4.1).

### 4.4 Final Program.cs Structure

```
Program.cs
├── WebApplication.CreateBuilder(args)
├── AddDbContext (MongoDB)
├── AddAuthentication + AddJwtBearer  (unchanged)
├── AddAuthorization()                 (unchanged)
├── AddScoped<AuthService>()          (unchanged)
├── AddScoped<OrderService>()          (unchanged)
├── AddGraphQLServer()                 (NEW)
├── Build()
├── UseDeveloperExceptionPage()
├── UseHttpsRedirection()
├── UseAuthentication()
├── UseAuthorization()
├── UseGraphQLPlayground()             (NEW)
├── UseGraphQLAsync<QueryResolver>()   (NEW)
├── SeedAsync (unchanged)
├── Run()
```

---

## Step 5: Update CLAUDE.md

Update the project architecture description to reflect GraphQL instead of REST.

---

## Step 6: Build and Verify

```bash
dotnet build
```

**Expected result:** `0 Warning(s), 0 Error(s)`

---

## Dependencies Summary

| Package | Version | Purpose |
|---------|---------|---------|
| `HotChocolate.AspNetCore` | 15.1.12 | GraphQL server middleware |
| `HotChocolate.Data` | 15.1.12 | Data filtering/pagination |
| `HotChocolate.Authorization` | 15.1.12 | `@authorize` directive |
| `HotChocolate.Types.OffsetPagination` | 15.1.12 | Offset-based pagination types |

## Risk Areas

1. **HotChocolate 15 + EF Core MongoDB Provider**: HotChocolate.Data is designed for `IQueryable` (Entity Framework, LINQ providers). MongoDB EF Core provider implements `IQueryable`, so HotChocolate.Data should work. If HotChocolate.Data.MongoDB 15.1.12 is needed, add it separately.
2. **JWT Authentication with GraphQL**: HotChocolate 15 supports ASP.NET Core authentication out-of-the-box via `UseAuthorizeDirective()`.
3. **Pagination**: `UsePagination` from HotChocolate requires the data source to implement `IPagedResults` or use `AddPaging()` extension.
