# GBA Trade License – .NET 10 Web API (Swagger + JWT + RBAC + OTP)

This is a **from-scratch starter backend** you can build on:
- ASP.NET Core **.NET 10** Web API
- Swagger UI with **Bearer JWT**
- Role-based authorization (Trader / Approver / SeniorApprover / Admin)
- OTP send/verify endpoints (server-side hashing + expiry)
- EF Core (SQL Server) + migrations + seed demo users

> ⚠️ You MUST replace secrets in `appsettings.json` before production.

## 0) Prerequisites
- .NET 10 SDK installed (net10.0)  
- SQL Server (local or remote)

## 1) Configure
Edit: `src/Gba.TradeLicense.Api/appsettings.json`
- `ConnectionStrings:Default`
- `Jwt:Key` (use a long random secret)
- `Otp:Secret` (optional but recommended; separate from JWT key)

## 2) Create Solution (optional)
From repo root:

```bash
dotnet new sln -n Gba.TradeLicense
dotnet sln add src/Gba.TradeLicense.Domain/Gba.TradeLicense.Domain.csproj
dotnet sln add src/Gba.TradeLicense.Application/Gba.TradeLicense.Application.csproj
dotnet sln add src/Gba.TradeLicense.Infrastructure/Gba.TradeLicense.Infrastructure.csproj
dotnet sln add src/Gba.TradeLicense.Api/Gba.TradeLicense.Api.csproj
```

## 3) Create DB + run migrations
Install EF tools (once):
```bash
dotnet tool install --global dotnet-ef
```

From `src/Gba.TradeLicense.Api`:
```bash
dotnet ef migrations add InitialCreate -p ../Gba.TradeLicense.Infrastructure -s .
dotnet ef database update -p ../Gba.TradeLicense.Infrastructure -s .
```

## 4) Run API
From `src/Gba.TradeLicense.Api`:
```bash
dotnet run
```
Open Swagger: `https://localhost:7181/swagger`

## 5) Demo users (seeded on startup)
These are created in `DbSeeder.cs` (change immediately):
- Trader: 9999999999 / Trader@123
- Approver: 9999999998 / Approver@123
- Senior: 9999999997 / Senior@123
- Admin: 9999999996 / Admin@123

## 6) Auth flow (how to test in Swagger)
1. POST `/api/auth/login` with Trader credentials  
   - returns `otpRequired=true`
2. POST `/api/auth/otp/send` with phone  
3. POST `/api/auth/otp/verify` with phone + otp  
   - returns `accessToken`
4. In Swagger “Authorize” -> paste `Bearer {token}`  
5. Call trader endpoints.

> OTP is not returned by API. Integrate SMS gateway in `AuthService.SendOtpAsync`.

## 7) Next work you must implement (real project needs this)
- Real application workflow states + assignment to approvers
- Payment gateway callbacks + receipt/certificate generation
- Inspection checklist masters per trade type
- Full audit logging with safe PII handling + size limits
- External integrations (GIS road width, GST/PAN/property tax etc.)
