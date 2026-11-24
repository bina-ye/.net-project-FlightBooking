# FlightBooking — שרת

## תיאור המערכת

מערכת הזמנת כרטיסי טיסה: Web API לניהול טיסות, מושבים והזמנות.
המערכת מאפשרת חיפוש וסינון טיסות, בחירת מושב פנוי, תשלום מדומה,
והשלמת הזמנה עם העדפות אוכל וכשרות ופרטי נוסע.

הפרויקט בנוי בארבע שכבות:

- `FlightBooking.Core` — ישויות, DTOs, enums, ממשקי השירותים והריפוזיטוריז,
  ו-`SeatFactory` שיוצר את מושבי הטיסה.
- `FlightBooking.Data` — `DbContext`, קונפיגורציות Fluent API, ריפוזיטוריז,
  migrations ו-seed data.
- `FlightBooking.Service` — לוגיקה עסקית, מיפוי ב-AutoMapper, שירות תשלום מדומה.
- `FlightBooking.API` — Controllers, Middleware, `Program.cs`.

**כיוון התלויות:** Core לא מפנה לאף פרויקט. Data מפנה ל-Core. Service מפנה
ל-Core. API מפנה ל-Service ול-Core, ומכיר את Data רק לצורך רישום השירותים
ב-`Program.cs`. ה-Controllers מזריקים שירותים בלבד — לא `DbContext`.

טכנולוגיות: .NET 9, ASP.NET Core Web API, EF Core 9, PostgreSQL,
JWT, AutoMapper, NLog, xUnit + Moq.

---

## המשאב המוגבל — המושב

המשאב שעליו מתחרים הוא **המושב**. אין שדה קיבולת על טיסה — מקום פנוי
הוא מקום פנוי, ומקור האמת היחיד הוא שורת המושב עצמה.

התחרות נוצרת בין הרגע שבו משתמש רואה מושב כפנוי ובוחר אותו, לבין הרגע
שבו ההזמנה נשמרת. בחלון הזה משתמש אחר יכול לתפוס את אותו מושב.

המערכת פותרת זאת ב-**optimistic concurrency**:

- לכל מושב יש `RowVersion` (`uint`) הממופה לעמודת המערכת `xmin` של
  PostgreSQL באמצעות `IsRowVersion()`.
- EF Core מוסיף את הערך הישן לתנאי ה-`WHERE` של ה-`UPDATE`.
- אם השורה השתנתה בינתיים — אף שורה לא מתעדכנת, ו-EF Core זורק
  `DbUpdateConcurrencyException`.
- החריגה נתפסת בשכבת ה-Service (`CreateAsync`, `UpdateAsync`, `CancelAsync`)
  ומוחזרת כ-**409 Conflict** עם הודעה ברורה למשתמש, ונרשמת ללוג ברמת Warning.

---

## זרימת ההזמנה (כפי שהמערכת פועלת בפועל)

1. **ולידציה** — הבקשה נבדקת: יש מושבים, אין מושב כפול, כל המושבים באותה טיסה.
   מושב תפוס או טיסה שהמריאה → 400/409, בלי לגעת בכסף.
2. **תפיסת המושבים** — המושבים מסומנים `Booked` ונשמרים ב-`SaveChangesAsync`.
   **כאן מכריע ה-`xmin`:** אם משתמש אחר תפס אחד מהמושבים בין הקריאה לשמירה,
   EF זורק `DbUpdateConcurrencyException`, הלקוח מקבל **409 — ולא חויב בכלל**.
3. **גבייה** — רק אם תפיסת המושבים הצליחה, מבוצעת גבייה דרך `IPaymentService`
   (`MockPaymentService` בפועל; אין סליקה אמיתית).
4. **כשל גבייה** — אם הגבייה נכשלה, המושבים משתחררים חזרה ל-`Available`,
   ההזמנה מסומנת `Cancelled`, והלקוח מקבל 400.

**התוצאה:** מי שמפסיד בתחרות על המושב לא משלם — אין תשלום יתום ואין צורך
בהחזר כספי. הצד הלקוח מציג הודעה ברורה כשהוא מקבל 409 ומציע לבחור מושב אחר.

---

## הוראות הרצה מקומית

### דרישות מקדימות

- .NET 9 SDK
- PostgreSQL 16 ומעלה (או Docker)

### הרמת בסיס הנתונים

```bash
docker run --name pg -e POSTGRES_PASSWORD=postgres -p 5432:5432 -d postgres:16
```

### הגדרת סודות

מחרוזת החיבור ומפתח ה-JWT אינם בקוד ואינם ב-repo. בפיתוח הם נשמרים ב-User Secrets.

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=flightbooking_dev;Username=postgres;Password=postgres" --project FlightBooking.API
dotnet user-secrets set "Jwt:Key" "<מפתח-בן-64-תוים>" --project FlightBooking.API
dotnet user-secrets set "Jwt:Issuer" "FlightBooking" --project FlightBooking.API
dotnet user-secrets set "Jwt:Audience" "FlightBooking.Client" --project FlightBooking.API
```

### הפעלה

```bash
dotnet restore
dotnet run --project FlightBooking.API
```

Swagger: `http://localhost:5069/swagger` (זמין בסביבת Development).

בעליית השרת מתבצעים אוטומטית `MigrateAsync` וטעינת נתוני דמו — שדות תעופה,
טיסות, מושבים ומשתמשים. ה-seed מדלג על עצמו אם כבר קיימים נתונים, כך שהרצה
חוזרת אינה דורסת. אין צורך בהתערבות ידנית בבסיס הנתונים.

### הרצת בדיקות

```bash
dotnet test
```

**39 בדיקות:** בדיקות יחידה ל-`BookingService` ו-`FlightService` (Moq),
בדיקות אינטגרציה ל-`FlightRepository` מול מסד InMemory, ובדיקות תחרות מול
PostgreSQL אמיתי.

בדיקות התחרות (`ConcurrencyTests`) מתחברות ל-PostgreSQL לפי משתנה הסביבה
`FLIGHTBOOKING_TEST_DB`. ללא המשתנה הן מדולגות ולא נכשלות:

```bash
export FLIGHTBOOKING_TEST_DB="Host=localhost;Port=5432;Database=flightbooking_test;Username=postgres;Password=postgres"
dotnet test
```

> הערה: בדיקות אלה יוצרות ובונות מחדש את בסיס הנתונים שמוגדר ב-
> `FLIGHTBOOKING_TEST_DB`, ולכן אין להצביע על בסיס נתונים בשימוש.

---

## משתמשי דמו

| תפקיד | אימייל | סיסמה |
|---|---|---|
| מנהל | admin@flightbooking.local | Password123! |
| לקוח | customer@flightbooking.local | Password123! |

---

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
| POST | `/api/flights` | מנהל | יצירת טיסה חדשה (עם 180 מושבים) |
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
| `includePast` | bool | false | טיסות שהמריאו — מתויג ל-false אלא אם המשתמש מנהל |

---

## הרשאות ובדיקתן בשרת

JWT עם שני תפקידים — `Admin` ו-`Customer`. ההרשאה נאכפת בשרת, לא בצד הלקוח:

- `[Authorize(Roles = "Admin")]` על יצירת טיסה.
- בקונטרולרים מוגני `[Authorize]`, הרשאת בעלים נבדקת ב-Service
  (`if (!isAdmin && booking.UserId != userId)`) וממופה ל-403.
- `includePast` בקטלוג מנוטרל (נאלץ ל-false) עבור משתמש שאינו מנהל.
- הרשמה תמיד יוצרת משתמש בתפקיד `Customer` — התפקיד אינו מגיע מהבקשה.

---

## Middleware

| Middleware | תפקיד |
|---|---|
| `CorrelationIdMiddleware` | מוסיף `X-Correlation-Id` לכל בקשה ותגובה ומכניס אותו ל-scope של הלוג |
| `ErrorHandlingMiddleware` | תופס חריגות לא צפויות ומחזיר ProblemDetails אחיד עם correlationId |
| `ShabbatMiddleware` | חוסם פעילות בשבת ומחזיר 503; כבוי כברירת מחדל ב-Development |

סדר ה-pipeline: CorrelationId → ErrorHandling → Shabbat → CORS → Swagger (בפיתוח)
→ ForwardedHeaders → HttpsRedirection → Authentication → Authorization → Controllers.

שגיאות עסקיות — מושב תפוס, טיסה שהמריאה, תשלום נכשל — אינן עוברות
כ-middleware. הן מוחזרות כ-`Result` מהשכבה העסקית וממופות לסטטוס HTTP
ב-Controller.

---

## לוגים

NLog מוגדר ב-`nlog.config` (נטען אוטומטית), והקוד משתמש ב-`ILogger` הרגיל של .NET.

- Information — כל בקשה נכנסת, עם ה-CorrelationId
- Warning — התנגשות על מושב, ניסיון התחברות שנכשל, תשלום שנדחה
- Error — חריגות שנתפסו

פלט לנתיב `FlightBooking.API/bin/<Configuration>/net9.0/logs/` בקובץ יומי
ובקידוד UTF-8, עם ארכיון של עד 30 קבצים (`maxArchiveFiles`).

סיסמאות, טוקנים ומספרי כרטיסים אינם נרשמים ללוג.

---

## החלטות תכנון

- PostgreSQL ולא SQL Server — תמיכה מובנית ב-`xmin` כ-concurrency token,
  בלי צורך בעמודת `rowversion` נוספת.
- תשלום מדומה — כרטיס המסתיים ב-`0000` נדחה, כל השאר מאושרים.
- תפיסה לפני גבייה — ראו "זרימת ההזמנה": מי שמפסיד בתחרות לא משלם.
- שמות עצם ברבים ופעולות HTTP תקניות, כולל 201 עם Location ו-204 בביטול.
- AutoMapper Profile — המיפוי של טיסות ומושבים מרוכז ב-`MappingProfile`;
  הרכבות של תגובות הזמנה ומשתמש נבנות ידנית ב-Service, כי הן כוללות חישובים
  (למשל `AvailableSeatsCount`, מיזוג סגמנטים, המרת enums למחרוזות) שאינם
  מיפוי שדה-לשדה פשוט.

### טרנזקציה בתפיסת המושבים

בדיקת הזמינות והשמירה מתבצעות באותה יחידת עבודה של `DbContext`: כל ה-`UPDATE`
של המושבים מוצאים ב-`SaveChangesAsync` אחד, שהוא טרנזקציה אטומית מטבעו, ותנאי
ה-`xmin` נבדק בתוך אותו `UPDATE`. אין `BeginTransactionAsync` מפורש, כי אין
צורך בו — אם אחד מהמושבים לא מתעדכן, אף מושב אחר לא מתעדכן, והחריגה מגלגלת את
כל השמירה לאחור. תוספת של טרנזקציה חיצונית הייתה מקשה על שחרור המושבים במקרה
של כשל גבייה, בלי להוסיף הגנה שכבר קיימת.

---

## הרצה בענן

המערכת פרוסה גם בענן (Render + PostgreSQL מנוהל), לבדיקה מקצה לקצה בלי
התקנה מקומית. הפריסה מבוססת על ה-`Dockerfile` שבשורש הפרויקט.

משתני הסביבה הנדרשים בפריסה:

- `ConnectionStrings__DefaultConnection` — מחרוזת החיבור ל-PostgreSQL
- `Jwt__Key` — מפתח החתימה של ה-JWT
- `ASPNETCORE_ENVIRONMENT` — `Production`

---

## צד הלקוח

הממשק נמצא ב-repo נפרד. יש להריץ אותו על `http://localhost:5500` (מוגדר ב-CORS)
או להוסיף את הפורט שלו ל-`Program.cs`. צד הלקוח אינו נבדק — הוא נדרש רק
כהוכחה שהשרת עובד מקצה לקצה, כולל הצגת הודעת 409 כשהוא מוחזר.

---

## מה לא נכלל

אחסון קבצים, אימות צד שלישי, caching, SignalR ו-CI — בהתאם לדרישות הפרויקט.
