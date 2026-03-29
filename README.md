# Banking API

A production-grade Banking REST API built with ASP.NET Core 8, CQRS via
Wolverine, Entity Framework Core 8, MySQL, and JWT authentication.

---

## Table of Contents

- [Architecture Overview](#architecture-overview)
- [Prerequisites](#prerequisites)
- [Project Structure](#project-structure)
- [Configuration](#configuration)
- [Database Setup](#database-setup)
- [Running the API](#running-the-api)
- [Running Tests](#running-tests)
- [API Endpoints](#api-endpoints)
- [Sample cURL Commands](#sample-curl-commands)
- [Transfer Flow](#transfer-flow)
- [Fee Schedule](#fee-schedule)
- [Security Notes](#security-notes)

---

## Architecture Overview
```
┌─────────────────────────────────────────────────────────────┐
│                        BankingApi.Api                       │
│          Controllers → Wolverine IMessageBus                │
└────────────────────────────┬────────────────────────────────┘
                             │ Commands / Queries
┌────────────────────────────▼────────────────────────────────┐
│                   BankingApi.Application                    │
│   Auth │ Accounts │ Transactions │ Exceptions │ Behaviors   │
│              Wolverine Handlers + FluentValidation          │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                  BankingApi.Infrastructure                  │
│   BankingDbContext │ JwtTokenService │ FeeCalculator        │
│   AccountNumberGenerator │ DatabaseSeeder                   │
└────────────────────────────┬────────────────────────────────┘
                             │
┌────────────────────────────▼────────────────────────────────┐
│                    BankingApi.Domain                        │
│          Entities: User, Account, Transaction               │
│          Enums: AccountType, NglPoolType, Gender …          │
└─────────────────────────────────────────────────────────────┘
```

**Pattern:** Vertical slice CQRS — each feature owns its Command/Query,
Validator, and Handler. No MediatR; Wolverine handles dispatch,
middleware, and transactional messaging.

**Ledger model:** Every fund transfer produces exactly **three** transaction
legs sharing one `Reference`:
```
Sender ──(Amount+Fee)──► NGL Credit ──(Fee)──► NGL Fee | NGL Debit ──(Amount)──► Recipient
  [CustomerTransfer]              [FeeCapture]                     [NGLDebit]
```

---

## Prerequisites

| Requirement | Minimum version |
|---|---|
| .NET SDK | 8.0 |
| MySQL Server | 8.0 |
| EF Core CLI tools | 8.0 (`dotnet tool install -g dotnet-ef`) |

Verify your environment:
```bash
dotnet --version      # 8.0.x
mysql --version       # 8.0.x
dotnet ef --version   # 8.0.x
```

---

## Project Structure
```
BankingApi/
├── src/
│   ├── BankingApi.Api/                  # ASP.NET Core Web API host
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json
│   │   └── Controllers/
│   │       ├── AuthController.cs
│   │       ├── AccountController.cs
│   │       └── TransactionController.cs
│   │
│   ├── BankingApi.Application/          # CQRS handlers, validators, exceptions
│   │   ├── Auth/Commands/
│   │   ├── Accounts/Commands|Queries/
│   │   ├── Transactions/Commands|Queries/
│   │   └── Common/Behaviors|Exceptions/
│   │
│   ├── BankingApi.Domain/               # Pure domain entities and enums
│   │   ├── Entities/
│   │   └── Enums/
│   │
│   └── BankingApi.Infrastructure/       # EF Core, services, seeder
│       ├── Persistence/
│       │   ├── BankingDbContext.cs
│       │   ├── Configurations/
│       │   └── Migrations/
│       ├── Services/
│       └── Seed/
│
├── tests/
│   └── BankingApi.Tests/
│       ├── Auth/
│       ├── Transactions/
│       └── Helpers/
│
├── db-scripts/
│   └── schema.sql                       # Raw MySQL DDL backup
└── BankingApi.sln
```

---

## Configuration

### 1. Clone the repository
```bash
git clone https://github.com/your-org/banking-api.git
cd banking-api
```

### 2. Configure appsettings

Open `src/BankingApi.Api/appsettings.json` and update:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Port=3306;Database=banking_db;User=root;Password=YOUR_MYSQL_PASSWORD;CharSet=utf8mb4;"
  },
  "Jwt": {
    "Key": "YourSuperSecretKeyHereMustBeAtLeast32CharactersLong!",
    "Issuer": "BankingApi",
    "ExpiresInMinutes": 60
  },
  "FeeSchedule": [
    { "MaxAmount": 5000.00,  "Fee": 10.00 },
    { "MaxAmount": 50000.00, "Fee": 25.00 },
    { "MaxAmount": null,     "Fee": 50.00 }
  ]
}
```

> **Security:** Never commit real credentials. Use
> `dotnet user-secrets` or environment variables in production.

### 3. User Secrets (recommended for local dev)
```bash
cd src/BankingApi.Api

dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Server=localhost;Port=3306;Database=banking_db;User=root;Password=secret;CharSet=utf8mb4;"
dotnet user-secrets set "Jwt:Key" "YourSuperSecretKeyHereMustBeAtLeast32CharactersLong!"
```

### 4. Environment variables (production)
```bash
export ConnectionStrings__DefaultConnection="Server=...;Password=...;"
export Jwt__Key="your-production-secret-key"
export ASPNETCORE_ENVIRONMENT="Production"
```

---

## Database Setup

### Option A — EF Core Migrations (recommended)
```bash
# 1. Create the database in MySQL first
mysql -u root -p -e "CREATE DATABASE banking_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;"

# 2. Apply all migrations
dotnet ef database update \
  --project src/BankingApi.Infrastructure \
  --startup-project src/BankingApi.Api

# 3. Seed data runs automatically on first startup in Development
#    (NGL system accounts + two test customers)
```

### Option B — Raw SQL script
```bash
mysql -u root -p banking_db < db-scripts/schema.sql
```

### Adding a new migration (after model changes)
```bash
dotnet ef migrations add YourMigrationName \
  --project src/BankingApi.Infrastructure \
  --startup-project src/BankingApi.Api \
  --output-dir Persistence/Migrations
```

### Rolling back to a previous migration
```bash
dotnet ef database update PreviousMigrationName \
  --project src/BankingApi.Infrastructure \
  --startup-project src/BankingApi.Api
```

### Removing the last unapplied migration
```bash
dotnet ef migrations remove \
  --project src/BankingApi.Infrastructure \
  --startup-project src/BankingApi.Api
```

---

## Running the API

### Development
```bash
cd src/BankingApi.Api
dotnet run
```

The API starts at:
- HTTP:  `http://localhost:5000`
- HTTPS: `https://localhost:5001`
- Swagger UI: `http://localhost:5000` (root — served at `/`)

### Production
```bash
dotnet publish src/BankingApi.Api -c Release -o ./publish
cd publish
ASPNETCORE_ENVIRONMENT=Production dotnet BankingApi.Api.dll
```
---

## Running Tests

### All tests
```bash
dotnet test
```

### With detailed output
```bash
dotnet test --logger "console;verbosity=detailed"
```

### Specific test project
```bash
dotnet test tests/BankingApi.Tests
```

### Specific test class
```bash
dotnet test --filter "FullyQualifiedName~TransferFundsHandlerTests"
```

### Specific test method
```bash
dotnet test --filter "FullyQualifiedName~Handle_ValidTransfer_CreatesThreeLegsWithSharedReference"
```

### With coverage (requires coverlet)
```bash
dotnet test --collect:"XPlat Code Coverage"
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report"
```

---

## API Endpoints

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/register` | ✗ | Register new customer |
| `POST` | `/api/auth/login` | ✗ | Login and get JWT token |
| `GET` | `/api/accounts/me` | ✓ | Get own account profile |
| `PUT` | `/api/accounts/me` | ✓ | Update own profile (PATCH semantics) |
| `POST` | `/api/transactions/transfer` | ✓ | Transfer funds to another account |
| `GET` | `/api/transactions/history` | ✓ | Get paginated transaction history |

**Query parameters for** `GET /api/transactions/history`:

| Parameter | Type | Default | Description |
|---|---|---|---|
| `pageNumber` | `int` | `1` | Page number (1-based) |
| `pageSize` | `int` | `20` | Results per page (max 100) |

---

## Sample cURL Commands

Replace `YOUR_TOKEN` with the JWT returned from `/api/auth/login`.

---

### Register
```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "firstName":  "Alice",
    "middleName": null,
    "lastName":   "Smith",
    "gender":     "Female",
    "email":      "alice@example.com",
    "password":   "Secure@123",
    "bvn":        "12345678902",
    "address":    "45 Adeola Odeku Street",
    "state":      "Lagos",
    "country":    "Nigeria"
  }'
```

**201 Created response:**
```json
{
  "userId":        "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fullName":      "Alice Smith",
  "email":         "alice@example.com",
  "accountNumber": "0000000005",
  "currency":      "NGN",
  "accountType":   "Customer",
  "createdAt":     "2024-07-01T10:00:00Z"
}
```

---

### Login
```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email":    "john@test.com",
    "password": "Test@1234"
  }'
```

**200 OK response:**
```json
{
  "token":     "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2024-07-01T11:00:00Z",
  "userId":    "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fullName":  "John Doe",
  "email":     "john@test.com"
}
```

---

### Get Account Profile
```bash
curl -X GET http://localhost:5000/api/accounts/me \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**200 OK response:**
```json
{
  "userId":           "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fullName":         "John Doe",
  "email":            "john@test.com",
  "gender":           "Male",
  "address":          null,
  "state":            null,
  "country":          "Nigeria",
  "accountNumber":    "0000000003",
  "maskedBVN":        "XXXXXXX8901",
  "balance":          500000.00,
  "currency":         "NGN",
  "accountType":      "Customer",
  "accountCreatedAt": "2024-07-01T00:00:00Z",
  "accountUpdatedAt": "2024-07-01T00:00:00Z",
  "userCreatedAt":    "2024-07-01T00:00:00Z"
}
```

---

### Update Account Profile
```bash
curl -X PUT http://localhost:5000/api/accounts/me \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "firstName": "Jonathan",
    "address":   "10 Broad Street",
    "state":     "Lagos"
  }'
```

**200 OK response:**
```json
{
  "userId":           "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "fullName":         "Jonathan Doe",
  "email":            "john@test.com",
  "gender":           "Male",
  "address":          "10 Broad Street",
  "state":            "Lagos",
  "country":          "Nigeria",
  "accountNumber":    "0000000003",
  "accountUpdatedAt": "2024-07-01T10:30:00Z"
}
```

---

### Transfer Funds
```bash
curl -X POST http://localhost:5000/api/transactions/transfer \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "destAccountNumber": "0000000004",
    "amount":            10000.00,
    "narration":         "Payment for services"
  }'
```

**200 OK response:**
```json
{
  "reference":              "TXN202407011030224F9A1B",
  "amount":                 10000.00,
  "fee":                    25.00,
  "totalDebited":           10025.00,
  "recipientAccountNumber": "0000000004",
  "senderAccountNumber":    "0000000003",
  "status":                 "Completed",
  "transactedAt":           "2024-07-01T10:30:22Z"
}
```

---

### Get Transaction History
```bash
curl -X GET "http://localhost:5000/api/transactions/history?pageNumber=1&pageSize=10" \
  -H "Authorization: Bearer YOUR_TOKEN"
```

**200 OK response:**
```json
{
  "pageNumber":  1,
  "pageSize":    10,
  "totalCount":  1,
  "totalPages":  1,
  "transactions": [
    {
      "reference":    "TXN202407011030224F9A1B",
      "amount":       10000.00,
      "fee":          25.00,
      "totalDebited": 10025.00,
      "status":       "Completed",
      "createdAt":    "2024-07-01T10:30:22Z",
      "legs": [
        {
          "id":                  "a1b2c3d4-...",
          "type":                "CustomerTransfer",
          "sourceAccountNumber": "0000000003",
          "destAccountNumber":   "0000000001",
          "amount":              10000.00,
          "fee":                 25.00,
          "totalDebited":        10025.00,
          "narration":           "Payment for services",
          "status":              "Completed",
          "createdAt":           "2024-07-01T10:30:22Z"
        },
        {
          "id":                  "b2c3d4e5-...",
          "type":                "FeeCapture",
          "sourceAccountNumber": "0000000001",
          "destAccountNumber":   "0000000002",
          "amount":              25.00,
          "fee":                 0.00,
          "totalDebited":        25.00,
          "narration":           "Fee settlement",
          "status":              "Completed",
          "createdAt":           "2024-07-01T10:30:22Z"
        },
        {
          "id":                  "c3d4e5f6-...",
          "type":                "NGLDebit",
          "sourceAccountNumber": "0000000002",
          "destAccountNumber":   "0000000004",
          "amount":              10000.00,
          "fee":                 0.00,
          "totalDebited":        10000.00,
          "narration":           "Payment for services",
          "status":              "Completed",
          "createdAt":           "2024-07-01T10:30:22Z"
        }
      ]
    }
  ]
}
```

---

### Validation error (422)
```json
{
  "type":   "https://tools.ietf.org/html/rfc7807",
  "title":  "Validation Failed",
  "status": 422,
  "detail": "One or more validation errors occurred.",
  "errors": {
    "email":    ["Email must be a valid email address."],
    "password": ["Password must contain at least one uppercase letter."]
  }
}
```

---

## Transfer Flow
```
POST /api/transactions/transfer
         │
         ▼
  Extract SenderId from JWT claim
         │
         ▼
  Validate command (FluentValidation middleware)
         │
         ▼
  Load sender account  (AccountType = Customer, UserId = SenderId)
         │
         ▼
  Load recipient account (AccountType = Customer, AccountNumber = dest)
         │
         ▼
  Guard: sender != recipient
         │
         ▼
  Calculate fee via IFeeCalculator
         │
         ▼
  Guard: sender.Balance >= Amount + Fee
         │
         ▼
  Load NGL Credit + NGL Debit accounts
         │
  ┌──────▼───────────────────────────────────────┐
  │  BEGIN DATABASE TRANSACTION                  │
  │                                              │
  │  LEG 1 — CustomerTransfer                    │
  │    Sender.Balance      -= Amount + Fee       │
  │    NglCredit.Balance   += Amount + Fee       │
  │                                              │
  │  LEG 2 — FeeCapture                          │
  │    NglCredit.Balance   -= Fee                │
  │    NglFee.Balance    += Fee                  │
  │                                              │
  │  LEG 3 — NGLDebit                            │
  │    NglDebit.Balance    -= Amount             │
  │    Recipient.Balance   += Amount             │
  │                                              │
  │  SaveChangesAsync()                          │
  │  COMMIT                                      │
  └──────────────────────────────────────────────┘
         │
         ▼
  Return TransferFundsResult
```

---

## Fee Schedule

Configured in `appsettings.json` under `FeeSchedule`.
Matches the first tier where `Amount <= MaxAmount`.
`MaxAmount: null` is the catch-all for any amount above the last tier.

| Transfer Amount | Fee |
|---|---|
| Up to ₦5,000 | ₦10.00 |
| ₦5,001 – ₦50,000 | ₦25.00 |
| Above ₦50,000 | ₦50.00 |

---

## Seed Data (Development only)

Seeded automatically on startup when `ASPNETCORE_ENVIRONMENT=Development`.
The seeder is idempotent — safe to restart repeatedly.

| Role | Email | Password | Account | Balance |
|---|---|---|---|---|
| NGL Credit (system) | ngl.credit@system.internal | System@NGL1 | 0000000001 | ₦0 |
| NGL Debit (system) | ngl.debit@system.internal | System@NGL2 | 0000000002 | ₦1,000,000,000 |
| NGL Fee (system) | ngl.fee@system.internal | System@NGL3 | 0000000003 | ₦0 |
| Test Customer | john@test.com | Test@1234 | 0000000004 | ₦500,000 |
| Test Customer | jane@test.com | Test@1234 | 0000000005 | ₦100,000 |

---

## Security Notes

- BCrypt work factor **12** for all passwords (factor **4** in tests for speed)
- JWT tokens expire after **60 minutes** (configurable)
- `ClockSkew` set to `TimeSpan.Zero` — no grace period on expiry
- BVN is **never** returned raw — always masked as `XXXXXXX{last4}`
- `Password` field is **never** included in any API response or DTO
- NGL system accounts (`IsSystemAccount = true`) are **never** returned
  by any customer-facing endpoint
- `SenderId` is **always** extracted from the JWT claim — never the
  request body, preventing impersonation
- All database queries use **EF Core parameterized queries** — no raw
  interpolated SQL anywhere
- Set `RequireHttpsMetadata = true` in `Program.cs` before deploying
  to production
