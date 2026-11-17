# FlightBooking — שרת

## תיאור המערכת

מערכת הזמנת כרטיסי טיסה: Web API לניהול טיסות, מושבים והזמנות.
המערכת מאפשרת חיפוש וסינון טיסות, בחירת מושב פנוי, תשלום מדומה,
והשלמת הזמנה עם העדפות אוכל וכשרות ופרטי נוסע.

הפרויקט בנוי בארבע שכבות: `FlightBooking.Core` (ישויות, DTOs, ממשקים),
`FlightBooking.Data` (DbContext, repositories, migrations),
`FlightBooking.Service` (לוגיקה עסקית), ו-`FlightBooking.API`
(Controllers, Middleware, `Program.cs`).

טכנולוגיות: .NET 9, ASP.NET Core Web API, EF Core 9, PostgreSQL,
JWT, AutoMapper, NLog, xUnit + Moq.

## המשאב המוגבל — המושב

המשאב שעליו מתחרים הוא **המושב**. אין שדה קיבולת על טיסה — מקום פנוי
הוא מקום פנוי, ומקור האמת היחיד הוא שורת המושב עצמה.

התחרות נוצרת בין הרגע שבו משתמש רואה מושב כפנוי ובוחר אותו, לבין הרגע
שבו ההזמנה נשמרת. בחלון הזה משתמש אחר יכול לתפוס את אותו מושב.

המערכת פותרת זאת ב-optimistic concurrency:

- לכל מושב יש `RowVersion` (`uint`) הממופה לעמודת המערכת `xmin` של
  PostgreSQL באמצעות `IsRowVersion()`.
- EF Core מוסיף את הערך הישן לתנאי ה-`WHERE` של ה-`UPDATE`.
- אם השורה השתנתה בינתיים — אף שורה לא מתעדכנת, ו-EF Core זורק
  `DbUpdateConcurrencyException`.
- החריגה נתפסת בשכבת ה-Service ומוחזרת כ-409 Conflict עם הודעה
  ברורה, והיא נרשמת ללוג ברמת Warning.

התשלום מתבצע לפני שמירת ההזמנה. במקרה של התנגשות נרשם לוג עם מזהה
העסקה; ב-`MockPaymentService` אין חיוב אמיתי ולכן החזר כספי אינו ממומש.

הצד הלקוח מציג הודעה ברורה כשהוא מקבל 409 ומציע לבחור מושב אחר.

## הוראות הרצה מקומית

### דרישות מקדימות

- .NET 9 SDK
- PostgreSQL 16 ומעלה (או Docker)

### הרמת בסיס הנתונים

```bash
docker run --name pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
```

### הגדרת סודות

מחרוזת החיבור ומפתח ה-JWT אינם בקוד ואינם ב-repo.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=flightbooking_dev;Username=postgres;Password=postgres" --project FlightBooking.API
dotnet user-secrets set "Jwt:Key" "<מפתח-בן-64-תוים>" --project FlightBooking.API
```

### הפעלה

```bash
dotnet restore
dotnet run --project FlightBooking.API
```

Swagger: `http://localhost:5069/swagger`

בעליית השרת מתבצעים אוטומטית migrations וטעינת נתוני דמו — שדות תעופה,
טיסות, מושבים ומשתמשים. אין צורך בהתערבות ידנית בבסיס הנתונים.

### הרצת בדיקות

```bash
dotnet test
```

כל 41 הבדיקות עוברות, כולל בדיקות התחרות מול PostgreSQL.

בדיקות התחרות מתחברות ל-PostgreSQL מקומי על `localhost:5432`. בסביבה
שבה החיבור שונה, יש להגדיר אותו במפורש:

```bash
export FLIGHTBOOKING_TEST_DB="Host=localhost;Port=5432;Database=flightbooking_test;Username=postgres;Password=postgres"
dotnet test
```

## משתמשי דמו

| תפקיד | אימייל | סיסמה |
|---|---|---|
| מנהל | admin@flightbooking.local | Password123! |
| לקוח | customer@flightbooking.local | Password123! |

## Endpoints

| Method | Path | הרשאה | תיאור |
|---|---|---|---|
| POST | `/api/auth/tokens` | אנונימי | התחברות, מחזיר JWT |
| POST | `/api/users` | אנונימי | הרשמה, מחזיר 201 עם Location |
| GET | `/api/users/me` | מאומת | פרופיל המשתמש המחובר |
| GET | `/api/users/{id}` | מאומת | פרטי משתמש; מנהל או המשתמש עצמו |
| GET | `/api/flights` | אנונימי | חיפוש טיסות עם עימוד |
| GET | `/api/flights/catalog` | אנונימי | קטלוג כל הטיסות עם סינון ועימוד |
| GET | `/api/flights/{id}` | אנונימי | פרטי טיסה |
| GET | `/api/flights/{id}/seats` | אנונימי | מושבי הטיסה וזמינותם |
| POST | `/api/flights` | מנהל | יצירת טיסה חדשה |
| POST | `/api/bookings` | מאומת | יצירת הזמנה, מחזיר 201 עם Location |
| GET | `/api/bookings/{id}` | בעלים או מנהל | פרטי הזמנה |
| GET | `/api/bookings/my` | מאומת | ההזמנות שלי עם עימוד |
| PUT | `/api/bookings/{id}` | בעלים או מנהל | עדכון הזמנה והחלפת מושבים |
| DELETE | `/api/bookings/{id}` | בעלים או מנהל | ביטול הזמנה, מחזיר 204 |

### פרמטרים של הקטלוג

| פרמטר | טיפוס | ברירת מחדל | תיאור |
|---|---|---|---|
| `pageNumber` | int | 1 | מספר עמוד |
| `pageSize` | int | 10 | גודל עמוד (עד 50) |
| `originCode` | string | — | קוד נמל מוצא (3 אותיות) |
| `destinationCode` | string | — | קוד נמל יעד (3 אותיות) |
| `fromDate` | date | — | מתאריך יציאה (YYYY-MM-DD) |
| `toDate` | date | — | עד תאריך יציאה (YYYY-MM-DD) |
| `includePast` | bool | false | טיסות שהמריאו — מנהל בלבד |

## בדיקות

41 בדיקות, כולן עוברות:

- בדיקות יחידה ל-`BookingService` ו-`FlightService` באמצעות Moq.
- בדיקות תחרות מול PostgreSQL אמיתי (`ConcurrencyTests`): שני
  `DbContext` נפרדים קוראים את אותו מושב, שניהם משנים אותו, הראשון
  שומר בהצלחה והשני מקבל `DbUpdateConcurrencyException`.

## Middleware

| Middleware | תפקיד |
|---|---|
| `CorrelationIdMiddleware` | מוסיף `X-Correlation-Id` לכל בקשה ותגובה ורושם אותו בכל שורת לוג |
| `ErrorHandlingMiddleware` | תופס חריגות לא צפויות ומחזיר ProblemDetails עם correlationId |
| `ShabbatMiddleware` | חוסם פעילות בשבת ומחזיר 503; כבוי כברירת מחדל ב-Development |

שגיאות עסקיות — מושב תפוס, טיסה שהמריאה, תשלום נכשל — אינן עוברות
כ-middleware. הן מוחזרות כ-`Result` מהשכבה העסקית וממופות לסטטוס HTTP
ב-Controller.

## לוגים

NLog כותב לנתיב `FlightBooking.API/bin/Debug/net9.0/logs/` בקובץ יומי,
בקידוד UTF-8, עם שמירה של 30 יום אחורה.

- Information — כל בקשה נכנסת, עם ה-CorrelationId
- Warning — התנגשות על מושב
- Error — חריגות שנתפסו

סיסמאות, טוקנים ומספרי כרטיסים אינם נרשמים.

## צד הלקוח

הממשק נמצא ב-repo נפרד. יש להריץ אותו על `http://localhost:5500`
(מוגדר ב-CORS) או להוסיף את הפורט שלו ל-`Program.cs`.

## הרצה בענן

המערכת פרוסה גם בענן, לבדיקה מקצה לקצה בלי התקנה מקומית.

- **API:** https://<your-app>.onrender.com
- **Swagger:** https://<your-app>.onrender.com/swagger
- **בסיס נתונים:** PostgreSQL מנוהל (Neon)

משתני הסביבה בפריסה:

- `ConnectionStrings__DefaultConnection` — מחרוזת החיבור ל-PostgreSQL
- `Jwt__Key` — מפתח החתימה של ה-JWT
- `ASPNETCORE_ENVIRONMENT` — `Production`

## החלטות תכנון

- PostgreSQL ולא SQL Server — תמיכה מובנית ב-`xmin` כ-concurrency token.
- תשלום מדומה — כרטיס המסתיים ב-0000 נדחה.
- שמות עצם ברבים ופעולות HTTP תקניות, כולל 201 עם Location ו-204 בביטול.
- JWT עם שני תפקידים — Admin ו-Customer, נאכף בשרת ולא רק בלקוח.
- AutoMapper Profile — המיפוי בין הישויות ל-DTOs מוגדר במקום אחד.

## מה לא נכלל

Docker לפריסה, אחסון קבצים, אימות צד שלישי, caching, SignalR ו-CI —
בהתאם לדרישות הפרויקט.
