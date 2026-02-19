using FinSyncNexus.Options;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

QuestPDF.Settings.License = LicenseType.Community;

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<FinSyncNexus.Data.AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("FinSyncNexusDb")));
builder.Services.AddHttpClient();

builder.Services.Configure<FinSyncNexus.Options.OAuthOptions>(
    builder.Configuration.GetSection("OAuth"));
builder.Services.Configure<AuthOptions>(
    builder.Configuration.GetSection("Auth"));
builder.Services.AddScoped<FinSyncNexus.Services.SyncService>();
builder.Services.AddScoped<FinSyncNexus.Services.XeroOAuthService>();
builder.Services.AddScoped<FinSyncNexus.Services.QboOAuthService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/auth/login";
        options.LogoutPath = "/auth/logout";
        options.AccessDeniedPath = "/auth/login";
    });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
