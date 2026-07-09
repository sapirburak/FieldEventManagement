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
            // תעשה את זה זמנית ב-Program.cs
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("12345678901234567890123456789012")),
           // IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
            ValidateIssuer = false, // זמנית עדיף להגדיר כ-false
            ValidateAudience = false,
            ValidateLifetime = true, // ודאי שזה דולק
            ClockSkew = TimeSpan.Zero // תגדירי אפס כדי לנטרל סטיות זמן של שרת
        };

        // הוספת ה-Events האלו תדפיס ב-Console של השרת בדיוק מה לא תקין
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var authHeader = context.Request.Headers["Authorization"].ToString();
                Console.WriteLine($"JWT OnMessageReceived. Raw Authorization Header: {authHeader}");

                var token = authHeader;
                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = token["Bearer ".Length..].Trim();
                }

                Console.WriteLine($"JWT Token Length: {token.Length}");
                Console.WriteLine($"JWT Token Preview: {token.Substring(0, Math.Min(token.Length, 40))}");
                Console.WriteLine($"JWT Token Has 3 Segments: {token.Split('.').Length == 3}");

                if (token.Split('.').Length >= 1)
                {
                    var headerSegment = token.Split('.')[0];
                    Console.WriteLine($"JWT Header Segment Preview: {headerSegment}");
                }

                return Task.CompletedTask;
            },
            OnAuthenticationFailed = context =>
            {
                Console.WriteLine($"Authentication failed: {context.Exception.Message}");
                var rawHeader = context.Request.Headers["Authorization"].ToString();
                Console.WriteLine($"Received Token Header: {rawHeader}");

                var token = rawHeader;
                if (token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = token["Bearer ".Length..].Trim();
                }

                Console.WriteLine($"Failed Token Length: {token.Length}");
                Console.WriteLine($"Failed Token Preview: {token.Substring(0, Math.Min(token.Length, 80))}");
                Console.WriteLine($"Failed Token Segment Count: {token.Split('.').Length}");
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                Console.WriteLine($"OnChallenge error: {context.Error}, {context.ErrorDescription}");
                return Task.CompletedTask;
            }
        };
    });
//.AddJwtBearer(options =>
//{
//    options.TokenValidationParameters = new TokenValidationParameters
//    {
//        ValidateIssuerSigningKey = true,
//        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),
//        ValidateIssuer = false,
//        ValidateAudience = false
//    };
//});

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
app.UseAuthentication(); // חובה: לוודא שזה מופיע לפני Authorization
app.UseAuthorization();
app.UseCors("AllowAngular");

// ב-Program.cs, לפני ה-app.UseAuthentication()
app.Use(async (context, next) =>
{
    var authHeader = context.Request.Headers["Authorization"].ToString();
    Console.WriteLine($"Incoming Authorization Header: {authHeader}");
    await next();
});

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