FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY OrderManagement.sln .
COPY OrderManagement.WebApi/ OrderManagement.WebApi/
COPY OrderManagement.Tests/ OrderManagement.Tests/

RUN dotnet restore
RUN dotnet publish OrderManagement.WebApi/OrderManagement.WebApi.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+8080
ENV ASPNETCORE_HTTPS_PORTS=8080

EXPOSE 8080

ENTRYPOINT ["dotnet", "OrderManagement.WebApi.dll"]
