using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebMessenger.Entities;

namespace WebMessenger
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add services to the container.
            builder.Services.AddControllersWithViews();

            // Настройка Entity Framework с SQLite
            builder.Services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

            // Настройка Identity
            builder.Services.AddIdentity<User, IdentityRole<int>>(options =>
            {
                options.User.AllowedUserNameCharacters =
                    "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789_";
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = true;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<AppDbContext>()
            .AddDefaultTokenProviders();

            // Настройка аутентификации через куки
            builder.Services.ConfigureApplicationCookie(options =>
            {
                options.Cookie.Name = "WebMessenger.Auth";
                options.Cookie.HttpOnly = true;
                options.ExpireTimeSpan = TimeSpan.FromDays(7);
                options.SlidingExpiration = true;
                options.LoginPath = "/api/auth/login";
                options.LogoutPath = "/api/auth/logout";
                options.AccessDeniedPath = "/api/auth/access-denied";
            });

            // Убираем проверку ролей в авторизации
            builder.Services.AddAuthorization(options =>
            {
                options.FallbackPolicy = null;
                options.AddPolicy("Authenticated", policy =>
                    policy.RequireAuthenticatedUser());
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

            app.UseAuthorization();

            app.MapStaticAssets();
            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}")
                .WithStaticAssets();

            using (var scope = app.Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                dbContext.Database.EnsureCreated(); // TODO: Заменить на миграцию
            }

            app.Run();
        }
    }
}
