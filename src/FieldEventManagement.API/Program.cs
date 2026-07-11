using FieldEventManagement.Application.Services;
using FieldEventManagement.Infrastructure.Persistence;
using FieldEventManagement.Core.Interfaces;
using FieldEventManagement.Infrastructure;
using FieldEventManagement.Infrastructure.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. רישום שירותי התשתית (DB ו-SignalR)
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection")!);


// 2. רישום שירותי ה-Application
builder.Services.AddScoped<EventReceiverService>();
builder.Services.AddScoped<TechnicianEventService>();
// 1. רישום שירות הטוקנים (Infrastructure)
builder.Services.AddScoped<ITokenService, TokenService>();

// 2. הגדרת האוטנטיקציה (כפי שראינו קודם)
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = true,
            ValidIssuer = "FieldEventSystem",
            ValidateAudience = true,
            ValidAudience = "FieldEventSystem",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        // SignalR אינו יכול לשלוח Authorization header ב-WebSocket.
        // הפתרון הסטנדרטי: ה-client מעביר את הtoken ב-query string (?access_token=...).
        // ה-event הזה קורא אותו ומכניס אותו לcontext כך שהאימות הרגיל יפעל.
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;
                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/EventHub"))
                {
                    context.Token = accessToken;
                }
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();
// 3. רישום SignalR
builder.Services.AddSignalR();
builder.Services.AddControllers();

// 1. הגדרת המדיניות (Policy)
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials());
});

var app = builder.Build();

// UseCors חייב להיות לפני UseAuthentication ו-UseAuthorization.
// בקשות Preflight (OPTIONS) עוברות דרך CORS לפני שמתבצע אימות,
// אחרת הדפדפן מקבל שגיאת CORS במקום 401 ברור.
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // EnsureCreated יוצר את ה-DB והסכמה אם לא קיימים — מתאים לסביבת dev ללא migrations.
    dbContext.Database.EnsureCreated();
}


app.MapControllers();
// הגדרת ה-Hub של SignalR שהוגדר ב-Infrastructure
app.MapHub<EventHub>("/EventHub");

app.Run();