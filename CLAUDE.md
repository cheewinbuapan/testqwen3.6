# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

This is a .NET 8 Web API project with GraphQL API using HotChocolate, MongoDB as the database with EF Core MongoDB provider, and Docker for local development.

## Solution Structure

```
<solution>
├── src/
│   └── <WebApiProject>/          # Main .NET 8 Web API (HotChocolate GraphQL)
├── tests/
│   └── <TestProject>/            # xUnit test project (GraphQL integration tests)
├── docker-compose.yml
├── Dockerfile
└── *.csproj
```

## Commands

### Build

```bash
dotnet build
```

### Run (without Docker)

```bash
dotnet run --project src/<WebApiProject>/<WebApiProject>.csproj
```

### Run with Docker Compose

```bash
docker-compose up --build
```

### Tests

```bash
dotnet test
```

Run a single test:

```bash
dotnet test --filter "FullyQualifiedName~<TestClassName>.<TestMethodName>"
```

Run tests in a specific test project:

```bash
dotnet test tests/<TestProject>/<TestProject>.csproj
```

## Architecture Notes

- The Web API exposes GraphQL endpoints via HotChocolate.
- All data access uses EF Core with the MongoDB provider.
- Integration tests in the xUnit project test GraphQL queries and mutations via HTTP requests.
- Local environment is managed with Docker Compose (MongoDB + API).
