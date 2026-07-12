# Field Event Management System

מערכת לניהול אירועי שטח בזמן אמת, המורכבת משלושה רכיבים עיקריים:

| רכיב | תיאור | טכנולוגיה |
|------|-------|-----------|
| **FieldEventManagement.Agent** | סוכן שטח שמקבל אירועים ממקורות חיצוניים ומעבירם ל-API | ASP.NET Core 10, SQLite |
| **FieldEventManagement.API** | שרת מרכזי לניהול אירועים, משתמשים והרשאות | ASP.NET Core 10, SQL Server, SignalR |
| **field-event-app** | ממשק משתמש גרפי לדיספצ'רים וטכנאים | Angular 17 |

---

## דרישות מוקדמות

לפני ההרצה יש לוודא שהתקנת את הכלים הבאים:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/) ו-npm
- [Angular CLI 17](https://angular.io/cli): `npm install -g @angular/cli@17`
- **SQL Server** – נדרש עבור ה-API (ראה הגדרת Connection String בסעיף הבא)

---

## ארכיטקטורה ופורטים

```
[מקור חיצוני / שטח]
        |
        | POST /api/agent/events  (X-Api-Key header)
        ▼
[Agent  :5093 / :7256]  ──── SQLite (מקומי) ────►  [API  :5279 / :7257]
                                                            |
                                                     SQL Server DB
                                                            |
                                                     SignalR EventHub
                                                            |
                                                   [Angular :4200]
```

---

## הגדרת מסד הנתונים (SQL Server)

פתח את הקובץ `src/FieldEventManagement.API/appsettings.json` ועדכן את ה-Connection String:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=<שם-השרת>;Database=FieldEventManagement;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

> מסד הנתונים (טבלאות) נוצר אוטומטית בהרצה הראשונה דרך `EnsureCreated`.  
> אין צורך להריץ migrations ידנית.

---

## הרצת המערכת

יש להפעיל את שלושת הרכיבים **בו-זמנית**, כל אחד בחלון טרמינל נפרד.

### 1. API – השרת המרכזי

```powershell
cd src\FieldEventManagement.API
dotnet run
```

השרת יאזין על:
- `http://localhost:5279`
- `https://localhost:7257`

### 2. Agent – סוכן השטח

```powershell
cd src\FieldEventManagement.Agent
dotnet run
```

הסוכן יאזין על:
- `http://localhost:5093`
- `https://localhost:7256`

> **הגדרות הסוכן** נמצאות ב-`src/FieldEventManagement.Agent/appsettings.json`:
> ```json
> "AgentSettings": {
>   "BackendUrl": "https://localhost:7257",
>   "ExpectedApiKey": "SuperSecretFieldAgentKey2026"
> }
> ```

### 3. Angular – ממשק המשתמש

```powershell
cd field-event-app
npm install
npm start
```

האפליקציה תפתח על `http://localhost:4200`.

---

## שליחת אירוע שטח (דוגמה)

כדי לסמלץ אירוע מהשטח, שלח `POST` לסוכן עם מפתח ה-API בכותרת:

```http
POST http://localhost:5093/api/agent/events
X-Api-Key: SuperSecretFieldAgentKey2026
Content-Type: application/json

{
  "title": "תקלת חשמל",
  "description": "הפסקת חשמל ברחוב הרצל 10",
  "source": "FieldSensor-01",
  "location": "תל אביב"
}
```

הסוכן מחזיר `202 Accepted` מיד – האירוע נשמר ב-SQLite ונשלח ל-API באופן אסינכרוני.

---

## הוספת משתמשים ראשוניים

מסד הנתונים נוצר ריק. לפני הכניסה למערכת יש להכניס משתמשי בסיס ידנית עם SQL Server Management Studio (SSMS) או כל כלי שאילתה אחר:

```sql
USE FieldEventManagement;

INSERT INTO Users (Id, Username, PasswordHash, Role) VALUES
  (NEWID(), 'dispatcher1', 'Password123', 'Scheduler'),
  (NEWID(), 'tech1',       'Password123', 'Technician'),
  (NEWID(), 'tech2',       'Password123', 'Technician');
```

> **שים לב:** השדה `PasswordHash` מאוחסן כטקסט חופשי במימוש הנוכחי (לצורך בחינה בלבד). בסביבת Production יש להשתמש ב-`PasswordHasher`.

---

## אימות (Login)

ה-API מוגן ב-JWT. לקבלת טוקן:

```http
POST http://localhost:5279/api/auth/login
Content-Type: application/json

{
  "username": "<שם-משתמש>",
  "password": "<סיסמה>"
}
```

הטוקן המוחזר יש לשלוח בכותרת `Authorization: Bearer <token>` בכל פנייה מוגנת.

---

## הרצת בדיקות

### בדיקות יחידה – Core (xUnit)

```powershell
cd src\FieldEventManagement.Core.Tests
dotnet test
```

### בדיקות – Angular (Karma)

```powershell
cd field-event-app
npm test
```

### הרצת כל בדיקות ה-.NET בבת אחת

```powershell
cd src
dotnet test
```

---

## מבנה הפרויקט

```
FieldEventManagementNew/
├── src/
│   ├── FieldEventManagement.Core/           # Domain – Entities, Interfaces, State Machine
│   ├── FieldEventManagement.Application/    # Application Services, DTOs
│   ├── FieldEventManagement.Infrastructure/ # EF Core, SQL Server, SignalR, JWT
│   ├── FieldEventManagement.API/            # ASP.NET Core Web API (שרת מרכזי)
│   ├── FieldEventManagement.Agent/          # ASP.NET Core Agent (סוכן שטח + SQLite)
│   └── FieldEventManagement.Core.Tests/     # Unit Tests – Core
└── field-event-app/                         # Angular 17 Frontend
```

---

## תכונות עיקריות

- **State Machine** – מעברי מצב מוגדרים בדיוק (`Unassigned → Assigned → InProgress → Completed / Cancelled`)
- **Store-and-Forward** – הסוכן שומר אירועים ב-SQLite ומשלח לאחר שחזור קישוריות
- **Real-Time** – עדכונים ישירים לדפדפן דרך SignalR (`/EventHub`)
- **Retry & Resilience** – הסוכן מנסה שוב עם Exponential Backoff ללא הגבלת ניסיונות עד להצלחה: כשל 1 → 10s | כשל 2 → 30s | כשל 3 → 60s | כשל 4+ → 5 דקות (גג). הצלחה מאפסת את הספירה
- **Audit Trail** – כל שינוי מצב מתועד עם חותמת זמן ומזהה המשתמש המבצע
