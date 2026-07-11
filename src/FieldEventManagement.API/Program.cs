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

// 1. Register infrastructure services (DB and SignalR)
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection")!);


// 2. Register Application services
builder.Services.AddScoped<EventReceiverService>();
builder.Services.AddScoped<TechnicianEventService>();
// Register the token service (Infrastructure)
builder.Services.AddScoped<ITokenService, TokenService>();

// Configure authentication
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

        // SignalR cannot send an Authorization header over WebSocket.
        // Standard solution: the client passes the token in the query string (?access_token=...).
        // This event reads it and injects it into the context so that normal authentication applies.
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
// 3. Register SignalR
builder.Services.AddSignalR();
builder.Services.AddControllers();

// Configure the CORS policy
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

// UseCors must come before UseAuthentication and UseAuthorization.
// Preflight (OPTIONS) requests go through CORS before authentication occurs;
// otherwise the browser receives a CORS error instead of a clear 401.
app.UseCors("AllowAngular");
app.UseAuthentication();
app.UseAuthorization();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // EnsureCreated creates the DB and schema if they don't exist — suitable for dev without migrations.
    dbContext.Database.EnsureCreated();
}


app.MapControllers();
// Map the SignalR Hub defined in Infrastructure
app.MapHub<EventHub>("/EventHub");

app.Run();
