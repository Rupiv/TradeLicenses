# Creates the solution file and adds projects (run from repo root)
dotnet new sln -n Gba.TradeLicense
dotnet sln add src/Gba.TradeLicense.Domain/Gba.TradeLicense.Domain.csproj
dotnet sln add src/Gba.TradeLicense.Application/Gba.TradeLicense.Application.csproj
dotnet sln add src/Gba.TradeLicense.Infrastructure/Gba.TradeLicense.Infrastructure.csproj
dotnet sln add src/Gba.TradeLicense.Api/Gba.TradeLicense.Api.csproj
