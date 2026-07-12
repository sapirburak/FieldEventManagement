# שאלת בונוס — תמיכה במצב Offline

> **גרסה:** 1.0 | **תאריך:** יולי 2026

---

## הגדרת הבעיה

הטכנאי בשטח עלול להיכנס לאזור ללא קליטה, להמשיך לעבוד (שינוי סטטוסים, הוספת הערות), ולחזור לקליטה אחרי פרק זמן בלתי ידוע. כל הפעולות שביצע במנותק צריכות להגיע לשרת — תוך טיפול בקונפליקטים מול שינויים שנעשו במקביל על ידי הסדרן.

---

## 1. אחסון מקומי בצד הלקוח

```
דפדפן הטכנאי
├── Service Worker         ← מיירט כל בקשת רשת, מאפשר PWA
├── Cache API              ← שומר קבצי Angular (JS, CSS, HTML)
└── IndexedDB              ← שומר נתונים מבניים
    ├── events             ← האירועים המוקצים לטכנאי
    ├── offline_queue      ← פעולות שבוצעו במנותק, ממתינות לשליחה
    └── sync_metadata      ← חותמת זמן אחרון של סנכרון
```

**למה IndexedDB ולא LocalStorage:**

| קריטריון | LocalStorage | IndexedDB |
|----------|:-----------:|:---------:|
| גודל מקסימלי | ~5MB | מאות MB |
| מבנה נתונים | מחרוזת בלבד | אובייקטים מבניים |
| חיפוש/אינדקס | ❌ | ✅ |
| עבודה א-סינכרונית | ❌ | ✅ |
| גישה מ-Service Worker | ❌ | ✅ |

כל פעולה שהטכנאי מבצע במנותק לא נשלחת לשרת — נרשמת כ-**Command** בתור:

```json
{
  "id": "uuid-local-1",
  "type": "UpdateStatus",
  "eventId": "event-abc",
  "payload": { "newStatus": "InProgress" },
  "timestamp": "2026-07-11T18:32:00Z",
  "clientVersion": 3
}
```

ה-`clientVersion` הוא גרסת האירוע שהטכנאי ראה לאחרונה — קריטי לזיהוי קונפליקטים.

---

## 2. סנכרון עם חזרת החיבור

```
טכנאי חוזר לקליטה
        │
        ▼
Service Worker מזהה חיבור רשת (online event)
        │
        ▼
שלב א: Pull — GET /api/events?assignedTo=me&since=<last_sync>
        │
        ▼
שלב ב: Conflict Detection — השווה clientVersion לגרסה בשרת
        │
        ▼
שלב ג: Push — POST /api/sync (batch של כל ה-offline_queue)
        │
        ▼
שלב ד: Resolve — טפל בקונפליקטים (אוטומטי או אנושי)
        │
        ▼
שלב ה: Clear Queue — נקה offline_queue
```

**פורמט בקשת הסנכרון:**

```http
POST /api/sync
Authorization: Bearer <jwt>

{
  "lastSyncTimestamp": "2026-07-11T18:00:00Z",
  "operations": [
    { "type": "UpdateStatus", "eventId": "abc", "clientVersion": 3, "payload": { "newStatus": "InProgress" } },
    { "type": "AddNote",      "eventId": "abc", "clientVersion": 3, "payload": { "text": "הדלת נעולה" } }
  ]
}
```

**תשובת השרת:**

```json
{
  "accepted": ["uuid-local-2"],
  "conflicts": [
    {
      "operationId": "uuid-local-1",
      "serverVersion": 5,
      "serverState": { "status": "Completed", "updatedBy": "dispatcher@company.com" },
      "clientState": { "status": "InProgress" }
    }
  ],
  "serverUpdates": []
}
```

---

## 3. טיפול בקונפליקטים

**קונפליקט** מתרחש כאשר גרסת האירוע אצל הטכנאי שונה מגרסת השרת — כלומר מישהו אחר שינה את האירוע בזמן הניתוק.

### שלוש גישות

**גישה 1 — Server-Wins:** השרת תמיד מנצח, שינויי הטכנאי נמחקים. פשוט לממש, חוויית משתמש גרועה.

**גישה 2 — Last-Write-Wins (timestamp):** מי ששינה אחרון מנצח. **בעייתי** — שעוני מכשירים לא מסונכרנים (clock drift).

**גישה 3 — Merge חכם לפי סוג פעולה (מומלצת):**

| סוג פעולה | גישה | הנמקה |
|-----------|------|--------|
| `AddNote` | מיזוג אוטומטי ✅ | Append-only — שתי ההערות נשמרות |
| `UpdateStatus` | התראה לטכנאי ⚠️ | State Machine יחיד — נדרשת החלטה אנושית |

**תרחיש קונקרטי:**

```
שרת (אחרי ניתוק הטכנאי):
  הסדרן העביר אירוע → Completed (גרסה 5)

טכנאי (מנותק):
  שינה אירוע → InProgress (ידע על גרסה 3)

בחזרה לקליטה:
  ├── Completed → InProgress? לא חוקי ב-State Machine
  ├── השרת דוחה
  └── UI מציג: "האירוע סומן כ-Completed על ידי הסדרן בזמן שהיית מנותק"
              [קבל] [פנה לסדרן]
```

---

## 4. שינויים נדרשים במערכת

| רכיב | שינוי |
|------|-------|
| **Angular** | Service Worker + IndexedDB wrapper + Sync Manager |
| **Angular** | Optimistic UI — עדכן ממשק מיד לפני אישור שרת |
| **API** | הוסף `POST /api/sync` שמקבל batch של פעולות |
| **API** | הוסף `ETag` / `RowVersion` לכל אירוע |
| **Domain** | `FieldEvent` יחזיק `RowVersion` לזיהוי גרסה |
| **DB** | הוסף עמודת `RowVersion` לטבלת Events |
| **SignalR** | שלח עדכונים לטכנאי עם גרסה לשמירה ב-IndexedDB |

---

> **עיקרון מרכזי:** הסוד של Offline הוא לא "לאחסן נתונים" — אלא **לאחסן פעולות** (Commands), לסנכרן לפי גרסאות ולא לפי שעון, ולהחליט מראש מה מתמזג אוטומטית ומה דורש התערבות אנושית.
