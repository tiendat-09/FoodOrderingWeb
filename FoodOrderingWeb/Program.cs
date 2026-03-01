using Microsoft.EntityFrameworkCore;
using FoodOrderingWeb.Models;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;

var builder = WebApplication.CreateBuilder(args);

// ==========================================
// 1. CẤU HÌNH DỊCH VỤ (SERVICES)
// ==========================================

// A. Database Context
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<FoodOrderingDbContext>(options =>
    options.UseSqlServer(connectionString));

// B. Cấu hình Session (QUAN TRỌNG CHO GIỎ HÀNG)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options => {
    options.IdleTimeout = TimeSpan.FromMinutes(60); // Giữ giỏ hàng trong 60 phút
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// C. HttpContextAccessor (Để truy cập Session ở View/Layout)
builder.Services.AddHttpContextAccessor();

// D. Authentication (Google & Cookie)
builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = GoogleDefaults.AuthenticationScheme;
})
.AddCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
})
.AddGoogle(options =>
{
    options.ClientId = "563061364839-5qafbf5fjhqf79ld6p5fnh4p4d2dce7s.apps.googleusercontent.com";
    options.ClientSecret = "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";
    options.CallbackPath = "/signin-google";
});

builder.Services.AddControllersWithViews();
var app = builder.Build();

// ==========================================
// 2. CẤU HÌNH MIDDLEWARE (PIPELINE)
// ==========================================

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

// [1] Routing (Phải chạy trước)
app.UseRouting();

// [2] Session (BẮT BUỘC PHẢI CÓ DÒNG NÀY ĐỂ GIỎ HÀNG CHẠY)
app.UseSession();

// [3] Authentication (Xác thực - Bạn là ai?)
app.UseAuthentication();

// [4] Authorization (Phân quyền - Bạn được làm gì?)
app.UseAuthorization();

// [5] Map Controller
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();