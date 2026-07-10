using FieldEventManagement.Application.Services;
using FieldEventManagement.Core.Interfaces;
using FieldEventManagement.Infrastructure; // בשביל ה-DependencyInjection שלנו
using FieldEventManagement.Infrastructure.Notifications;
using FieldEventManagement.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// 1. רישום שירותי התשתית (DB ו-SignalR)
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection")!);


// 2. רישום ה-Service של ה-Application
builder.Services.AddScoped<EventReceiverService>();
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
            ValidateIssuer = false,
            ValidateAudience = false,
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
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular",
        policy => policy
            .WithOrigins("http://localhost:4200") // הכתובת של האנגולר שלך
            //.WithOrigins()
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials()); // קריטי ל-SignalR!
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
    // פקודה זו בודקת אם ה-DB קיים, ואם לא – היא יוצרת אותו.
    // אם ה-DB קיים, היא מריצה מיגרציות חסרות.
    dbContext.Database.EnsureCreated();
}


app.MapControllers();
// הגדרת ה-Hub של SignalR שהוגדר ב-Infrastructure
app.MapHub<EventHub>("/EventHub");

app.Run();