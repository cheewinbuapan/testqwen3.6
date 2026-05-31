# Agent Instructions

## Project Snapshot

This repository is a .NET 8 backend-only Order Management API. It exposes GraphQL at `/graphql` using HotChocolate 15.1.12, stores data in MongoDB through EF Core, and uses JWT bearer authentication. There is no frontend or web UI.

For product requirements, data model details, and GraphQL examples, link to [docs/spec.md](docs/spec.md). For the REST-to-GraphQL migration notes, link to [docs/migration.md](docs/migration.md). [CLAUDE.md](CLAUDE.md) contains a longer legacy agent brief and may duplicate the spec.

## Commands

- Build: `dotnet build`
- Test: `dotnet test`
- Run API locally: `dotnet run --project OrderManagement.WebApi/OrderManagement.WebApi.csproj`
- Run MongoDB + API: `docker-compose up --build`
- GraphQL endpoint in Docker: `http://localhost:8080/graphql`

## Code Map

- [OrderManagement.WebApi/Program.cs](OrderManagement.WebApi/Program.cs) wires MongoDB EF Core, JWT auth, HotChocolate schema types, GraphQL middleware, and startup seed data.
- [OrderManagement.WebApi/Models](OrderManagement.WebApi/Models) contains entities, `ApplicationDbContext`, `AuthService`, `OrderService`, and seed data.
- [OrderManagement.WebApi/GraphTypes](OrderManagement.WebApi/GraphTypes) contains HotChocolate code-first inputs, outputs, query root, and mutation root.
- [OrderManagement.Tests](OrderManagement.Tests) is an xUnit test project, currently minimal.

## Local Conventions

- Keep changes scoped to the GraphQL API; do not add frontend assets unless the user explicitly asks.
- Prefer HotChocolate code-first patterns already in `GraphTypes`: fluent `ObjectType` descriptors for root query/mutation fields, simple POCO input/output types for schema objects.
- Keep business logic in `AuthService` and `OrderService`; keep GraphQL types focused on schema mapping, argument extraction, authorization, and returning service results.
- Preserve UTC timestamps with `DateTime.UtcNow` for entity creation and updates.
- Use the existing role strings `Customer` and `Admin` consistently with JWT claims and GraphQL `.Authorize(...)` calls.
- When adding new C# types, prefer one class per `.cs` file as requested in [CLAUDE.md](CLAUDE.md); do not mechanically split existing grouped files unless the task calls for that refactor.

## Known Gaps And Pitfalls

- `OrderManagement.WebApi/Models/Services.cs` is empty; active services live in `AuthService.cs` and `OrderService.cs`.
- The current order service checks product stock but does not deduct stock when creating or updating orders.
- `UpdateOrderAsync` does not currently validate stock the same way `CreateOrderAsync` does.
- `SearchOrdersAsync` compares status strings to `PENDING` and `CONFIRMED`; GraphQL enum string conversion may produce `Pending` or `Confirmed`, so verify status filtering when touching search.
- `GenerateOrderNumber()` blocks on `CountAsync(...).Result`; be careful with async deadlocks or race conditions if changing order number generation.
- Tests are not yet representative of the GraphQL surface. Add focused tests when changing service behavior, authorization, or GraphQL schema contracts.

## Validation Guidance

- For code changes, run `dotnet build` first and `dotnet test` when tests are relevant or changed.
- For Docker-related changes, use `docker-compose up --build` to verify the MongoDB/API integration path.
- For GraphQL changes, exercise the `/graphql` endpoint with the mutation/query shape from [docs/spec.md](docs/spec.md).