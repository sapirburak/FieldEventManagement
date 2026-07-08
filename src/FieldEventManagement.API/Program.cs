using FieldEventManagement.Application.Services;
using FieldEventManagement.Infrastructure; // בשביל ה-DependencyInjection שלנו
using FieldEventManagement.Infrastructure.Notifications;
using FieldEventManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// 1. רישום שירותי התשתית (DB ו-SignalR)
builder.Services.AddInfrastructure(builder.Configuration.GetConnectionString("DefaultConnection")!);


// 2. רישום ה-Service של ה-Application
builder.Services.AddScoped<EventReceiverService>();

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
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    // פקודה זו בודקת אם ה-DB קיים, ואם לא – היא יוצרת אותו.
    // אם ה-DB קיים, היא מריצה מיגרציות חסרות.
    dbContext.Database.EnsureCreated();
}

app.UseCors("AllowAngular");

app.MapControllers();
// הגדרת ה-Hub של SignalR שהוגדר ב-Infrastructure
app.MapHub<EventHub>("/EventHub");

app.Run();