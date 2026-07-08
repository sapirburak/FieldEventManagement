using FieldEventManagement.Application.Interfaces;
using FieldEventManagement.Infrastructure.Notifications;
using FieldEventManagement.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FieldEventManagement.Infrastructure;
/// <summary>
/// מחלקת עזר (Extension Methods) להזרקת תלויות (Dependency Injection).
/// מרכזת את כלל שירותי התשתית של המערכת, מה שמאפשר ל-API להזריק את כל ה-Infrastructure 
/// באמצעות שורת קוד אחת בלבד.
/// </summary>
public static class DependencyInjection
{/// <summary>
 /// מבצע רישום של שירותי התשתית (DB Context, Repositories, Notifications) למכולת ה-DI של ה-ASP.NET.
 /// </summary>
 /// <param name="connectionString">מחרוזת ההתחברות למסד הנתונים.</param>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        // 1. הזרקת ה-DB
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));

        // 2. הזרקת ה-Repositories
        services.AddScoped<IFieldEventRepository, FieldEventRepository>();

        // 3. הזרקת שירות ההתראות
        services.AddScoped<IRealTimeNotificationService, RealTimeNotificationService>();

        return services;
    }
}