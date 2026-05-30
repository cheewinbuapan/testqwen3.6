# Order Management System — Product Specification

## 1. Overview

### 1.1 Business Context

ระบบ Backend สำหรับจัดการคลังสินค้าและคำสั่งซื้อ (Order Management System) สำหรับร้านค้า E-commerce ขนาดเล็กที่ต้องการความรวดเร็วและประสิทธิภาพสูง

### 1.2 Technology Stack

| Component | Technology |
|---|---|
| Backend Framework | .NET 8 Web API |
| GraphQL | HotChocolate v14 |
| Database | MongoDB (via EF Core MongoDB Provider) |
| Auth | JWT Bearer |
| Container | Docker + Docker Compose |
| ORM | Entity Framework Core 8 |

### 1.3 Non-Web UI

ระบบนี้ประกอบด้วย **Backend API เท่านั้น** ไม่มี Frontend/Web UI
API Endpoint: `/graphql`

---

## 2. Data Model

### 2.1 Entity: User

| Field | Type | Constraint |
|---|---|---|
| Id | ObjectId | Primary Key |
| Email | string | **Unique**, Required, Index |
| FirstName | string | Required |
| LastName | string | Required |
| Phone | string | Required |
| PasswordHash | string | Required, Hashed |
| Role | string | Default: `"Customer"` |
| CreatedAt | DateTime | Auto |
| UpdatedAt | DateTime | Auto |

**Key Note:** `Email` เป็น User Account สำหรับ Login แต่ **ไม่ใช่ Primary Key** (Primary Key คือ `Id`)

### 2.2 Entity: Product

| Field | Type | Constraint |
|---|---|---|
| Id | ObjectId | Primary Key |
| ProductNumber | string | **Unique**, Required |
| Name | string | Required |
| Price | decimal | Required, >= 0 |
| Stock | int | Required, >= 0 |
| ProductStatusId | ObjectId | Reference |

### 2.3 Entity: Order

| Field | Type | Constraint |
|---|---|---|
| Id | ObjectId | Primary Key |
| OrderNumber | string | **Unique**, Auto-generated (e.g., `"ORD-20260530-0001"`) |
| CustomerId | ObjectId | Reference to User |
| CustomerName | string | Concat: FirstName + LastName |
| ShippingAddress | string | Nullable |
| Status | OrderStatus | Enum, Default: `Pending` |
| TotalAmount | decimal | Computed |
| Items | List<OrderItem> | |
| CreatedAt | DateTime | Auto |
| UpdatedAt | DateTime | Auto |

### 2.4 Entity: OrderItem

| Field | Type | Constraint |
|---|---|---|
| Id | ObjectId | Primary Key |
| OrderId | ObjectId | Reference |
| ProductId | ObjectId | Reference |
| ProductNumber | string | Denormalized |
| ProductName | string | Denormalized |
| Quantity | int | Required, > 0 |
| UnitPrice | decimal | Snapshot at time of order |
| SubTotal | decimal | Computed |

### 2.5 Enum: OrderStatus

| Value | Thai |
|---|---|
| `Pending` | รอยืนยันคำสั่งซื้อ |
| `Confirmed` | ยืนยันคำสั่งซื้อ |

---

## 3. Seed Data

### 3.1 Auto Seed on Application Startup

ระบบจะทำการ Seed Data อัตโนมัติเมื่อแอปพลิเคชันเริ่มต้น (`Program.cs`)

### 3.2 Product Status Reference Data

```json
[
  { "Code": "PENDING", "DisplayName": "รอยืนยันคำสั่งซื้อ" },
  { "Code": "CONFIRMED", "DisplayName": "ยืนยันคำสั่งซื้อ" }
]
```

### 3.3 Sample Product Data

```json
[
  {
    "ProductNumber": "PROD-001",
    "Name": "สินค้าตัวอย่าง 1",
    "Price": 299.00,
    "Stock": 100
  },
  {
    "ProductNumber": "PROD-002",
    "Name": "สินค้าตัวอย่าง 2",
    "Price": 590.00,
    "Stock": 50
  },
  {
    "ProductNumber": "PROD-003",
    "Name": "สินค้าตัวอย่าง 3",
    "Price": 1250.00,
    "Stock": 30
  }
]
```

---

## 4. GraphQL Schema

### 4.1 Authentication

ใช้ JWT Bearer Token ผ่าน HTTP Header:
```
Authorization: Bearer <token>
```

---

### 4.2 Mutation: Authentication

#### 4.2.1 Create User Account

```graphql
mutation CreateUser($input: CreateUserInput!) {
  createUser(input: $input) {
    id
    email
    firstName
    lastName
    phone
    role
    createdAt
  }
}
```

**Input:**

| Field | Type | Required | Validation |
|---|---|---|---|
| Email | String | Yes | Format, Unique |
| FirstName | String | Yes | Min 1 char |
| LastName | String | Yes | Min 1 char |
| Phone | String | Yes | Format |
| Password | String | Yes | Min 8 chars |
| ConfirmPassword | String | Yes | Must match Password |

**Response:** User object หรือ Error

**Error Cases:**
- Email ซ้ำ → `"Email already exists"`
- ConfirmPassword ไม่ตรง → `"Passwords do not match"`
- Validation อื่น ๆ → 400 Bad Request

#### 4.2.2 User Login

```graphql
mutation Login($email: String!, $password: String!) {
  login(email: $email, password: $password) {
    token
    user {
      id
      email
      firstName
      lastName
      role
    }
  }
}
```

**Response:**

| Field | Type |
|---|---|
| token | string (JWT) |
| user | User object |

**Error Cases:**
- Email ไม่ถูกต้อง → 401 Unauthorized
- Password ไม่ถูกต้อง → 401 Unauthorized

---

### 4.3 Mutation: Order Management

#### 4.3.1 Create Order

```graphql
mutation CreateOrder($input: CreateOrderInput!) {
  createOrder(input: $input) {
    orderNumber
    customerName
    status
    totalAmount
    items {
      productNumber
      productName
      quantity
      unitPrice
      subTotal
    }
    createdAt
  }
}
```

**Input:**

| Field | Type | Required | Description |
|---|---|---|---|
| CustomerId | ObjectId | Yes | ผู้สั่งซื้อ |
| Items | [OrderItemInput!]! | Yes | รายการสินค้า (สามารถมีได้หลายชิ้น) |

**OrderItemInput:**

| Field | Type | Required |
|---|---|---|
| ProductId | ObjectId | Yes |
| Quantity | Int | Yes, > 0 |

**Response:** Order (มี OrderNumber ที่ auto-generate)

**Business Logic:**
- ตรวจสอบ Stock เพียงพอ
- คำนวณ TotalAmount จาก Product.Price * Quantity
- Auto-generate OrderNumber (รูปแบบ: `ORD-{YYYYMMDD}-{Sequence}`)

#### 4.3.2 Update Order (Admin)

```graphql
mutation UpdateOrder($id: ID!, $input: UpdateOrderInput!) {
  updateOrder(id: $id, input: $input) {
    orderNumber
    status
    totalAmount
    items {
      productNumber
      productName
      quantity
      unitPrice
    }
  }
}
```

**Input:**

| Field | Type | Required | Description |
|---|---|---|---|
| Items | [OrderItemInput!]! | Yes | รายการสินค้าใหม่ (Product Number + Quantity) |

**Validation:**
- เฉพาะ Order ที่อยู่ในสถานะ `Pending` เท่านั้นที่แก้ไขได้
- Stock ต้องเพียงพอ
- ไม่สามารถแก้ไข Order ที่ `Confirmed` ได้

#### 4.3.3 Confirm Order (User)

```graphql
mutation ConfirmOrder($id: ID!, $shippingAddress: String!) {
  confirmOrder(id: $id, shippingAddress: $shippingAddress) {
    orderNumber
    status
    shippingAddress
    customerName
  }
}
```

**Validation:**
- เฉพาะ Order ที่อยู่ในสถานะ `Pending` เท่านั้น
- ผู้สั่งซื้อ (Customer) เท่านั้นที่สามารถ Confirm Order ของตนเองได้
- ShippingAddress ไม่สามารถเป็น null หรือ empty ได้

**Status Transition:** `Pending` → `Confirmed`

---

### 4.4 Query: Order Search (Admin)

```graphql
query SearchOrders($filter: OrderFilterInput!) {
  searchOrders(filter: $filter) {
    totalCount
    orders {
      orderNumber
      customerName
      status
      totalAmount
      itemCount
      createdAt
    }
  }
}
```

**Filter Input:**

| Field | Type | Description |
|---|---|---|
| OrderNumber | String | ค้นหาด้วย Order Number |
| CustomerName | String | ค้นหาด้วยชื่อ-นามสกุล ผู้สั่ง |
| Status | OrderStatus | กรองตามสถานะ |
| Page | Int | หน้า (Default: 1) |
| PageSize | Int | จำนวนต่อหน้า (Default: 20, Max: 100) |

**Response:**

| Field | Type | Description |
|---|---|---|
| totalCount | Int | จำนวนทั้งหมด |
| orders | [OrderSummary!]! | รายการ Order |

**OrderSummary Fields:**

| Field | Type |
|---|---|
| orderNumber | String |
| customerName | String |
| status | OrderStatus |
| totalAmount | Decimal |
| itemCount | Int |
| createdAt | DateTime |

---

### 4.5 Mutation: Admin Bulk Update Status

```graphql
mutation BulkUpdateOrderStatus($ids: [ID!]!, $status: OrderStatus!) {
  bulkUpdateOrderStatus(ids: $ids, status: $status) {
    succeeded
    failed
    results {
      id
      orderNumber
      previousStatus
      newStatus
      success
      errorMessage
    }
  }
}
```

**Description:**
- Admin สามารถเลือก Order หลาย ๆ Order พร้อมกัน
- เปลี่ยนสถานะเป็น `Confirmed` (หรือสถานะอื่น ๆ ที่มีในอนาคต)
- Return ผลลัพธ์แยกแต่ละ Order (บางส่วนสำเร็จ บางส่วนล้มเหลว)

**Validation:**
- เฉพาะ Order ที่อยู่ในสถานะ `Pending` ที่สามารถ Bulk Confirm ได้
- Admin เท่านั้นที่เรียกใช้ Mutation นี้ได้

---

## 5. Authorization

### 5.1 Role-Based Access Control

| Role | Capabilities |
|---|---|
| `Customer` | Create Account, Login, Create Order, Confirm Own Order |
| `Admin` | ทุกอย่าง + Manage Orders, Search Orders, Bulk Update Status |

### 5.2 Policy Rules

1. **Create Order**: ต้อง Login (Customer เท่านั้น)
2. **Confirm Order**: Customer สามารถ Confirm เฉพาะ Order ของตนเอง
3. **Update Order**: Admin เท่านั้น
4. **Search Orders**: Admin เท่านั้น
5. **Bulk Update Status**: Admin เท่านั้น

---

## 6. API Error Handling

| HTTP Code | Meaning | Example |
|---|---|---|
| 400 | Bad Request | Validation Error |
| 401 | Unauthorized | ไม่ Login / Token หมดอายุ |
| 403 | Forbidden | ไม่มีสิทธิ์ |
| 404 | Not Found | ไม่พบ Resource |
| 409 | Conflict | Email ซ้ำ / Stock ไม่พอ |
| 500 | Internal Server Error | |

---

## 7. Project Structure

```
testqwen3.6/
├── src/
│   └── OrderManagement.WebApi/
│       ├── Properties/
│       ├── Program.cs
│       ├── appsettings.json
│       ├── Data/
│       │   ├── SeedData.cs
│       │   └── ApplicationDbContext.cs
│       ├── Entities/
│       │   ├── User.cs
│       │   ├── Product.cs
│       │   ├── Order.cs
│       │   ├── OrderItem.cs
│       │   └── OrderStatus.cs (enum)
│       ├── DTOs/
│       │   ├── Auth/
│       │   │   ├── CreateUserInput.cs
│       │   │   ├── LoginInput.cs
│       │   │   └── UserResponse.cs
│       │   ├── Order/
│       │   │   ├── CreateOrderInput.cs
│       │   │   ├── UpdateOrderInput.cs
│       │   │   ├── ConfirmOrderInput.cs
│       │   │   └── OrderFilterInput.cs
│       │   └── Product/
│       │       └── ProductInput.cs
│       ├── Services/
│       │   ├── AuthService.cs
│       │   ├── OrderService.cs
│       │   ├── ProductService.cs
│       │   └── IAuthService.cs
│       ├── GraphTypes/
│       │   ├── Inputs/
│       │   ├── Outputs/
│       │   └── TypeExtensions/
│       ├── Queries/
│       │   └── OrderQuery.cs
│       └── Mutations/
│           ├── AuthMutation.cs
│           ├── OrderMutation.cs
│           └── AdminMutation.cs
├── tests/
│   └── OrderManagement.Tests/
│       ├── Unit/
│       ├── Integration/
│       └── E2E/
├── docker-compose.yml
├── Dockerfile
├── docs/
│   ├── spec.md          ← ไฟล์นี้
│   └── req/
│       └── tor.txt
└── *.sln
```

---

## 8. Development Plan

### Phase 1: Project Setup (Day 1-2)

- [ ] Create .NET 8 Web API solution
- [ ] Configure Docker + Docker Compose (MongoDB + API)
- [ ] Setup Entity Framework Core with MongoDB provider
- [ ] Configure dependency injection

### Phase 2: Data Layer (Day 2-3)

- [ ] Define Entities (User, Product, Order, OrderItem)
- [ ] Implement DbContext / ApplicationDbContext
- [ ] Implement Seed Data (Product Status, Sample Products)
- [ ] Write Unit Tests for Entities

### Phase 3: Authentication (Day 3-5)

- [ ] Implement JWT Bearer Authentication
- [ ] GraphQL Mutation: CreateUser
- [ ] GraphQL Mutation: Login
- [ ] Password Hashing (BCrypt)
- [ ] Write Integration Tests

### Phase 4: Product Management (Day 5-6)

- [ ] Seed Data auto-load
- [ ] GraphQL Query: Get Product List
- [ ] GraphQL Mutation: Create Product (Admin)
- [ ] Write Integration Tests

### Phase 5: Order Management (Day 6-9)

- [ ] GraphQL Mutation: Create Order (with OrderItem)
- [ ] GraphQL Mutation: Update Order (Admin)
- [ ] GraphQL Mutation: Confirm Order (User)
- [ ] GraphQL Query: Search Orders (Admin)
- [ ] OrderNumber auto-generation logic
- [ ] Stock validation logic
- [ ] Write Integration Tests

### Phase 6: Admin Bulk Operations (Day 9-10)

- [ ] GraphQL Mutation: Bulk Update Order Status
- [ ] Authorization Policies (Admin vs Customer)
- [ ] Write Integration Tests

### Phase 7: Testing & Deployment (Day 10-12)

- [ ] End-to-end API tests
- [ ] Docker deployment tests
- [ ] API Documentation (GraphQL Playground)
- [ ] Performance review

---

## 9. API Response Format

### 9.1 Success

```json
{
  "data": {
    "createUser": {
      "id": "605c1cd0e15c1e0000d9a1b2",
      "email": "test@example.com",
      "firstName": "ทดสอบ",
      "lastName": "ตัวอย่าง",
      "phone": "0812345678",
      "role": "Customer",
      "createdAt": "2026-05-30T10:00:00Z"
    }
  }
}
```

### 9.2 Error

```json
{
  "errors": [
    {
      "message": "Email already exists",
      "extensions": {
        "code": "DUPLICATE_EMAIL",
        "fields": ["email"]
      }
    }
  ]
}
```

---

## 10. Order Number Format

```
ORD-{YYYYMMDD}-{Sequence}
```

**ตัวอย่าง:**
- `ORD-20260530-0001`
- `ORD-20260530-0002`
- `ORD-20260531-0001`

Sequence รีเซ็ตทุกวัน (daily sequence)

---

## 11. Order Status Flow

```
[Pending] ──[Confirm/BulkConfirm]──> [Confirmed]
   ↑                                        │
   │────────[Update (Admin)]────────────────┘
```

- **Pending** = รอยืนยันคำสั่งซื้อ
- **Confirmed** = ยืนยันคำสั่งซื้อ

---

## 12. Assumptions & Constraints

1. ระบบเป็น **E-commerce ขนาดเล็ก** — ไม่ต้องรองรับ high-scale
2. MongoDB เป็น Database หลัก — ใช้ EF Core MongoDB Provider
3. JWT Token หมดอายุใน **24 ชั่วโมง**
4. Password ใช้ **BCrypt** hashing
5. Order Number เป็น **string** (ไม่ใช้ numeric เพื่อรองรับ daily sequence)
6. Stock deduction/check — ตรวจสอบ stock ตอนสร้าง/แก้ไข Order
7. ไม่มี Payment Gateway integration ในเฟสนี้
8. ไม่มี Inventory tracking แบบ real-time
