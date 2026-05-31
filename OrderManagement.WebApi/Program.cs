using Microsoft.EntityFrameworkCore;
using OrderManagement.WebApi.GraphTypes.Inputs;
using OrderManagement.WebApi.GraphTypes.Mutations;
using OrderManagement.WebApi.GraphTypes.Outputs;
using OrderManagement.WebApi.GraphTypes.Queries;
using OrderManagement.WebApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMongoDB(
        builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017",
        "OrderManagementDb"));

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["JwtSettings:Issuer"] ?? "OrderManagement",
            ValidAudience = builder.Configuration["JwtSettings:Audience"] ?? "OrderManagement",
            IssuerSigningKey = new Microsoft.IdentityModel.Tokens.SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(builder.Configuration["JwtSettings:SecretKey"] ?? "secret"))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<OrderService>();
builder.Services.AddScoped<ProductService>();

builder.Services.AddGraphQLServer()
    .ConfigureSchema(schema => schema.AddAuthorizeDirectiveType())
    .AddQueryType<QueryType>()
    .AddFiltering()
    .AddSorting()
    .AddMutationType<MutationType>()
    .AddType<UserType>()
    .AddType<OrderType>()
    .AddType<OrderSummaryType>()
    .AddType<OrderStatusType>()
    .AddType<AuthOutputType>()
    .AddType<BulkUpdateResultType>()
    .AddType<ResultItemType>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGraphQL();

using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    await SeedData.SeedAsync(context);
}

app.Run();
