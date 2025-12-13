using Microsoft.EntityFrameworkCore;
using TradeLicensesModels.Data;


var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();
var app = builder.Build();

// Add DbContext Dependency Injection
builder.Services.AddDbContext<TlDataContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"))
);

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();


app.Run();
