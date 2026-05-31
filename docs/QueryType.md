# QueryType Pattern

เอกสารนี้สรุปรูปแบบการเขียน GraphQL Query ในโปรเจกต์ Order Management API โดยอ้างอิงจาก `QueryType` ปัจจุบัน และแนวทางการใช้ `[UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]` ของ HotChocolate

## ภาพรวม

โปรเจกต์นี้ใช้ HotChocolate code-first pattern โดยรวม GraphQL query root ไว้ที่ `OrderManagement.WebApi/GraphTypes/Queries/QueryType.cs` แล้ว register root type ผ่าน `Program.cs`

แนวทางหลักคือ:

- `QueryType` เป็นชั้น GraphQL resolver สำหรับรับ argument, กำหนดชื่อ field, authorization, paging, filtering, sorting และ map result เป็น output model
- `Service` เป็นชั้น business/data query logic และคืนค่า `IQueryable<T>` เมื่อ query ต้องรองรับ paging/filter/sort จาก HotChocolate
- `Output model` เป็น contract ที่ส่งออกไปหา client ไม่ควร return entity ตรง ๆ ถ้ามี field ภายในที่ไม่ต้องการ expose เช่น `CreatedAt`, `UpdatedAt`, `PasswordHash`
- `Program.cs` ต้อง register GraphQL root type, output type, service และ middleware ที่เกี่ยวข้อง

## โครงสร้างไฟล์ที่เกี่ยวข้อง

```text
OrderManagement.WebApi/
├── Program.cs
├── GraphTypes/
│   ├── Queries/
│   │   └── QueryType.cs
│   └── Outputs/
│       ├── ProductOutput.cs
│       ├── ProductOutputType.cs
│       └── OrderSummary.cs
└── Models/
    ├── ProductService.cs
    ├── OrderService.cs
    ├── DataContext.cs
    └── Entities.cs
```

## QueryType ทำหน้าที่อะไร

`QueryType` คือ GraphQL query root ของระบบ ตัวอย่าง field ปัจจุบัน:

- `searchOrders` สำหรับ Admin ค้นหา order พร้อม paging/filter/sort
- `getProducts` สำหรับดึงสินค้าแบบ paging โดยคืน `ProductOutput` แทน entity `Product`

ตัวอย่าง pattern:

```csharp
[GraphQLName("getProducts")]
[UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
public IQueryable<ProductOutput> GetProducts(
    [Service] ProductService productService)
{
    return productService.GetProductsQuery()
        .Select(product => new ProductOutput
        {
            Id = product.Id,
            ProductNumber = product.ProductNumber,
            Name = product.Name,
            Price = product.Price,
            Stock = product.Stock
        });
}
```

จุดสำคัญ:

- ใช้ `[GraphQLName("getProducts")]` เพื่อกำหนดชื่อ field ที่ client เรียก
- ใช้ `[Service] ProductService productService` เพื่อ inject service ผ่าน DI
- return `IQueryable<ProductOutput>` เพื่อให้ HotChocolate จัดการ paging ต่อได้
- map entity `Product` เป็น `ProductOutput` เพื่อควบคุม field ที่ client เห็น

## การใช้ UsePaging

Attribute นี้:

```csharp
[UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
```

ทำให้ field กลายเป็น cursor pagination แบบ connection pattern

ผลลัพธ์ที่ client query ได้จะมีรูปแบบ:

```graphql
query GetProducts {
  getProducts(first: 10) {
    totalCount
    nodes {
      id
      productNumber
      name
      price
      stock
    }
    pageInfo {
      hasNextPage
      hasPreviousPage
      startCursor
      endCursor
    }
  }
}
```

ความหมายของ options:

- `MaxPageSize = 100` จำกัดจำนวนสูงสุดต่อ request ไม่เกิน 100 records
- `IncludeTotalCount = true` เปิดให้ client ขอ `totalCount` ได้
- `first` คือจำนวนรายการที่ต้องการดึง
- `after` คือ cursor สำหรับดึงหน้าถัดไป

ตัวอย่างดึงหน้าถัดไป:

```graphql
query GetProductsNext {
  getProducts(first: 10, after: "<endCursor จากรอบก่อน>") {
    nodes {
      id
      productNumber
      name
      price
      stock
    }
    pageInfo {
      hasNextPage
      endCursor
    }
  }
}
```

## Service ควรทำอย่างไร

Service ควรรับผิดชอบการเข้าถึงข้อมูลและ business query logic โดยคืน `IQueryable<T>` สำหรับ query ที่ต้องให้ HotChocolate จัดการ paging/filter/sort

ตัวอย่าง `ProductService`:

```csharp
public class ProductService
{
    private readonly ApplicationDbContext _context;

    public ProductService(ApplicationDbContext context)
    {
        _context = context;
    }

    public IQueryable<Product> GetProductsQuery()
    {
        return _context.Products.AsQueryable();
    }
}
```

แนวทาง:

- อย่าเรียก `ToList()`, `ToListAsync()`, `First()` หรือ execute query เร็วเกินไป ถ้า resolver ต้องใช้ `[UsePaging]`
- ให้ service คืน `IQueryable<Product>` แล้วให้ `QueryType` map ต่อเป็น output model
- ถ้ามี filter พื้นฐานที่เป็น business rule เช่น active product เท่านั้น ให้ใส่ใน service ได้
- ถ้าเป็น field shaping สำหรับ client เช่น ตัดวันที่ออก ให้ทำผ่าน output model ใน GraphQL layer

## Output Model

ไม่ควร return entity ตรง ๆ เมื่อ entity มี field ที่ไม่ต้องการส่งออก client

ตัวอย่าง `Product` entity มี `CreatedAt` และ `UpdatedAt`:

```csharp
public class Product
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string ProductNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

ให้สร้าง output model ใหม่ที่มีเฉพาะ field ที่ต้องการ expose:

```csharp
public class ProductOutput
{
    public string Id { get; set; } = string.Empty;
    public string ProductNumber { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Stock { get; set; }
}
```

และสร้าง GraphQL type:

```csharp
public class ProductOutputType : HotChocolate.Types.ObjectType<ProductOutput>
{
}
```

## Program.cs Registration

ทุก service และ GraphQL type ที่ resolver ใช้ต้อง register ใน `Program.cs`

ตัวอย่าง registration ที่เกี่ยวข้อง:

```csharp
builder.Services.AddScoped<ProductService>();

builder.Services.AddGraphQLServer()
    .ConfigureSchema(schema => schema.AddAuthorizeDirectiveType())
    .AddQueryType<QueryType>()
    .AddFiltering()
    .AddSorting()
    .AddMutationType<MutationType>()
    .AddType<ProductOutputType>();
```

ข้อควรระวังสำหรับโปรเจกต์นี้:

- ใช้ fluent root registration ผ่าน `.AddQueryType<QueryType>()` และ `.AddMutationType<MutationType>()`
- ไม่ต้องใส่ `[QueryType]` บน class `QueryType`
- ไม่ต้องใส่ `[MutationType]` บน class `MutationType`
- ถ้าใส่ attribute root type ซ้ำกับ fluent registration อาจทำให้ runtime schema creation error ได้

## Pattern สำหรับเพิ่ม Query ใหม่

เมื่อต้องเพิ่ม GraphQL query ใหม่ ให้ทำตามลำดับนี้:

1. สร้าง output model ใน `GraphTypes/Outputs` ถ้าไม่ต้องการ expose entity ตรง ๆ
2. สร้าง `ObjectType<T>` สำหรับ output model ถ้าต้องการ register type ชัดเจน
3. เพิ่ม method ใน `QueryType`
4. Inject service ด้วย `[Service]`
5. ให้ service คืน `IQueryable<TEntity>` ถ้าต้องใช้ paging/filter/sort
6. Map entity เป็น output model ใน resolver ด้วย `.Select(...)`
7. Register service และ output type ใน `Program.cs`
8. Run `dotnet build` เพื่อตรวจ compile และ schema typing

ตัวอย่าง template:

```csharp
[GraphQLName("getSomething")]
[UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
public IQueryable<SomethingOutput> GetSomething(
    [Service] SomethingService somethingService)
{
    return somethingService.GetSomethingQuery()
        .Select(item => new SomethingOutput
        {
            Id = item.Id,
            Name = item.Name
        });
}
```

## Authorization, Filtering, Sorting

ถ้า query ต้องจำกัดสิทธิ์ ให้ใส่ `[Authorize]` ที่ method:

```csharp
[Authorize(Roles = new[] { "Admin" })]
[GraphQLName("searchOrders")]
[UsePaging(MaxPageSize = 100, IncludeTotalCount = true)]
[UseFiltering]
[UseSorting]
public IQueryable<OrderSummary> GetSearchOrders(...)
```

แนวทาง:

- query ที่ public เช่น `getProducts` ไม่ต้องใส่ `[Authorize]`
- query สำหรับ Admin เช่น `searchOrders` ใส่ `[Authorize(Roles = new[] { "Admin" })]`
- ใช้ `[UseFiltering]` และ `[UseSorting]` เฉพาะ field ที่ต้องให้ client filter/sort เอง
- ถ้า filter เป็น business-specific argument เช่น `orderNumber`, `customerName`, `status` สามารถรับ argument ใน method แล้วส่งต่อเข้า service ได้

## Checklist

- `QueryType` ไม่ return entity ที่มี sensitive/internal fields ถ้า client ไม่ควรเห็น
- resolver ที่ใช้ paging return `IQueryable<T>`
- service ไม่ execute query ก่อนถึง HotChocolate pipeline
- output model มีเฉพาะ field ที่ต้องการ expose
- service ถูก register ด้วย `AddScoped`
- output type ถูก register ด้วย `.AddType<...>()`
- root query ถูก register ด้วย `.AddQueryType<QueryType>()`
- ไม่ใช้ `[QueryType]` ซ้ำบน class root query
- หลังแก้ไข run `dotnet build`