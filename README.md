# מערכת ניהול פניות (Requests Management)

מערכת לניהול פניות: .NET 9 Web API מעל MongoDB, ו-Angular 21 בצד הלקוח.
המערכת כוללת שליפה server-side על 100K+ רשומות (דפדוף, סינון, חיפוש, מיון ו-aggregations), עדכון סטטוס עם
Optimistic Concurrency והיסטוריית audit, עדכון bulk עם Partial Success, ו-cache לסיכום.

- תוכנית העבודה וחלוקת המשימות: [WORK_PLAN.md](WORK_PLAN.md)
- כללי הסגנון והארכיטקטורה: [CONVENTIONS.md](CONVENTIONS.md)

---

## 1. הרצה

### דרישות מוקדמות
| כלי | גרסה | הערה |
|-----|------|------|
| Docker Desktop | עדכני | מריץ את MongoDB כ-replica set |
| .NET SDK | 9.0.x | נבדק עם 9.0.319 |
| Node.js | `^20.19` / `^22.12` / `^24` | דרישת Angular 21. נבדק עם 24.22.1 |

### צעדים
```powershell
# 1. MongoDB (single-node replica set — נדרש ל-transactions). --wait ממתין עד שה-healthcheck מדווח PRIMARY.
docker compose up -d --wait

# 2. ה-API ‏(http://localhost:5000, Swagger ב-/swagger). בעלייה נוצרים האינדקסים אוטומטית.
cd RequestsManagement.BE
dotnet run --project src/RequestsManagement.Api --launch-profile http

# 3. יצירת נתוני הבדיקה: 100,000 פניות (טרמינל נוסף; Development בלבד; כ-15 שניות).
#    reset=true מוחק את הקיים (פניות + היסטוריה) ויוצר מחדש בדיוק 100K, כך שאפשר להריץ שוב ושוב. בלי reset — מוסיף.
Invoke-RestMethod -Method Post "http://localhost:5000/api/seed?count=100000&reset=true"

# 4. ה-Frontend ‏(http://localhost:4200)
cd RequestsManagement.FE
npm ci
npm start
```

### בדיקות
```powershell
cd RequestsManagement.BE
dotnet test          # 40 בדיקות unit + 14 בדיקות integration
```
בדיקות ה-integration רצות מול ה-MongoDB של docker-compose (שלב 1 חייב לרוץ). כל מחלקת בדיקות יוצרת database
זמני משלה ומוחקת אותו בסוף, כך שנתוני ה-100K לא נפגעים. connection string אחר: משתנה הסביבה `MONGO_TEST_CONNECTION_STRING`.

### בלי Docker: MongoDB Atlas
כל cluster של Atlas הוא replica set, ולכן transactions עובדים. מספיק להחליף את ה-connection string:
```powershell
$env:Mongo__ConnectionString = "mongodb+srv://<user>:<password>@<cluster>/?retryWrites=true"
```

### איפוס
`docker compose down` עוצר את Mongo והנתונים נשמרים ב-volume. `docker compose down -v` מוחק גם את הנתונים.

---

## 2. טכנולוגיות וגרסאות
| שכבה | טכנולוגיה |
|------|-----------|
| API | .NET 9 / ASP.NET Core, Controllers, Swashbuckle 7.2 |
| BL | FluentValidation 11.11, `Microsoft.Extensions.Caching.Memory` 9.0 |
| DAL | MongoDB.Driver 3.8.0 |
| DB | MongoDB 7.0 (single-node replica set ב-Docker) |
| FE | Angular 21.2, Angular Material 21.2, RxJS 7.8, TypeScript 5.9 |
| בדיקות | xUnit 2.9, `Microsoft.AspNetCore.Mvc.Testing` 9.0 |

---

## 3. מבנה הפתרון
```
RequestsManagement/
├─ docker-compose.yml            MongoDB replica set + healthcheck
├─ scripts/perf-explain.js       מדידת ביצועים (explain) — סעיף 6
├─ RequestsManagement.BE/
│  ├─ src/
│  │  ├─ RequestsManagement.Api  Controllers, Program.cs, GlobalExceptionHandler, CORS, hosted service לאינדקסים
│  │  ├─ RequestsManagement.Bl   Services, Validators, Caching, Exceptions
│  │  ├─ RequestsManagement.Dal  Context, Models, Repositories, Indexes, Mapping, Seed
│  │  └─ RequestsManagement.Dto  DTOs, Enums + מכונת המצבים, Paging, ApiResponseDto<T>
│  └─ tests/
│     ├─ RequestsManagement.UnitTests         BL: concurrency, ולידציה, bulk, cache
│     └─ RequestsManagement.IntegrationTests  API + Mongo אמיתי
└─ RequestsManagement.FE/        Angular (core/http, features/requests, shared)
```

**כיווני תלות** (חד-כיווניים): Api → Bl + Dto, ‏Bl → Dal + Dto, ‏Dal → Dto. ה-API מחזיר DTOs בלבד ולעולם לא ישות DB.
כל תגובה עטופה ב-`ApiResponseDto<T>` (`success`/`data`/`error`). שגיאות מוחזרות כ-ProblemDetails (RFC 7807) מ-handler גלובלי.

**Lifetimes:** `MongoConnectionFactory`, ‏`AppMongoDbContext` ו-`RequestsSummaryCache` הם Singleton. Repositories ו-Services הם Scoped.

**Frontend:** `RequestListComponent` הוא ה-smart container היחיד. הוא מחזיק את מצב השאילתה ב-signals ומתזמר את ה-service.
הסיכום, פאנל הסינון והטבלה הם רכיבי תצוגה (`input()`/`output()` בלבד). `RequestsService` יורש מ-`BaseHttpService`, פותח את ה-envelope,
ומחזיק את מצב הסיכום ב-signals.

---

## 4. API

| Method | Route | תיאור |
|--------|-------|-------|
| GET | `/api/requests` | שליפה: `page`, `pageSize` (≤100), `status`, `priority`, `organizationName`, `assignedTo`, `createdFrom`, `createdTo`, `search`, `sortBy`, `sortDirection` |
| GET | `/api/requests/summary` | aggregations: סך הכל, לפי סטטוס, לפי עדיפות (מגיע מה-cache) |
| GET | `/api/requests/{id}` | פנייה בודדת (משמש לרענון אחרי 409) |
| PATCH | `/api/requests/{id}/status` | `{ newStatus, expectedVersion, changedBy? }` → 200 / 400 / 404 / 409 |
| PATCH | `/api/requests/bulk-status` | `{ newStatus, items: [{ id, expectedVersion }], changedBy? }` → דוח פר-פנייה |
| GET | `/api/requests/{id}/history` | היסטוריית שינויי סטטוס, מהחדש לישן |
| POST | `/api/seed?count=100000&reset=true` | יצירה מחדש של נתוני הבדיקה. בלי `reset` מוסיף לקיים (Development בלבד) |
| GET | `/api/health` | בדיקת חיות + ping ל-Mongo |

**מכונת המצבים:** New → InProgress/Waiting, ‏InProgress → Waiting/Completed, ‏Waiting → InProgress/Completed, ‏Completed הוא מצב סופי.

**שדות מיון מותרים (whitelist):** `createdAt` (ברירת מחדל, יורד), `updatedAt`, `priority`, `status`, `organizationName`, `title`.

**שגיאות קלט:** כל שגיאת קלט מוחזרת כ-400 באותו פורמט: `ValidationProblemDetails` עם הכותרת "שגיאות ולידציה", ושגיאות לפי שם שדה בעברית.
זה כולל גם שגיאות שנכשלות עוד לפני הוולידציה, ב-model binding: JSON שבור, ‏`page=abc`, או סטטוס לא מוכר כמו `"Foo"`.
במקרה של enum, ההודעה מפרטת את הערכים המותרים.
מעבר סטטוס לא חוקי הוא כלל עסקי ולא שגיאת קלט, ולכן מוחזר כ-ProblemDetails עם `allowedTransitions`.
בקשות לדוגמה לכל ה-endpoints: `RequestsManagement.Api.http`.

---

## 5. מודל הנתונים ואינדקסים

**`requests`:** `Title`, `OrganizationName`, `Status`, `Priority`, `AssignedTo`, `CreatedAt`, `UpdatedAt` (UTC), ו-`Version` (int, ל-Optimistic Concurrency).
**`statusAudit`:** `RequestId`, `PreviousStatus`, `NewStatus`, `ChangedAt`, `ChangedBy`. ה-collection הזה append-only.

האינדקסים נוצרים אוטומטית בעליית ה-API (`MongoIndexInitializer`). אם הגדרת אינדקס השתנתה, האינדקס הישן נמחק ונוצר מחדש.

| אינדקס | מפתחות | משרת את |
|--------|--------|---------|
| `ix_status_priority_createdAt` | Status, Priority, CreatedAt↓, _id↓ | סינון סטטוס+עדיפות ממוין לפי תאריך (המסך הנפוץ), ספירה מסוננת |
| `ix_createdAt` | CreatedAt↓, _id↓ | מיון ברירת המחדל, טווח תאריכים |
| `ix_updatedAt` / `ix_status` / `ix_priority` / `ix_title` | השדה, _id | מיון לפי כל שדה ב-whitelist |
| `ix_organizationName` | OrganizationName, _id | סינון ומיון לפי ארגון |
| `ix_assignedTo` | AssignedTo | סינון לפי משויך |
| `tx_title_organizationName` | text(Title, OrganizationName) | חיפוש טקסט |
| `ix_requestId_changedAt` (audit) | RequestId, ChangedAt↓ | היסטוריית פנייה |

**למה כל אינדקס מיון מסתיים ב-`_id`:** המיון באפליקציה הוא תמיד (שדה, ‏`_id`). בלי שובר שוויון, בשדה עם ערכים חוזרים
(בעדיפות יש 3 ערכים בלבד, ו-8,916 פניות חולקות `CreatedAt` עם פנייה אחרת) Mongo לא מבטיח סדר, ו-skip/limit חותך את הדפים במקום
אחר בכל בקשה. נמדד: 40 עמודים לפי עדיפות החזירו 578 כפילויות מתוך 1,000 שורות, ופניות אחרות לא הופיעו כלל. אחרי התיקון: 0 כפילויות.

**Enums נשמרים כ-Int32 ולא כמחרוזות.** בגרסה הראשונה הם נשמרו כמחרוזות, כדי שיהיה קל לקרוא אותם ב-DB. אבל על מחרוזות, מיון לפי עדיפות הוא אלפביתי
(High < Low < Medium). מספרים נותנים את סדר ה-enum. ה-API עדיין מחזיר ומקבל שמות (`JsonStringEnumConverter`).

---

## 6. ביצועים (100,000 רשומות)

### איך נמנעת טעינה של כל הנתונים לזיכרון
כל סינון, חיפוש, מיון ודפדוף מתבצע ב-MongoDB, כחלק מאותה שאילתה (`Find(filter).Sort(...).Skip(...).Limit(pageSize)`).
לשרת מגיעים רק המסמכים של העמוד המבוקש (עד 100), ול-FE רק ה-DTOs שלהם. ‏`totalCount` מחושב ב-DB ומוחזר כמספר.
ה-aggregations של הסיכום רצים ב-DB (`$facet`) ומחזירים 7 מספרים בלבד. ב-FE אין מיון או סינון בזיכרון: הטבלה מציגה את מה שהשרת החזיר.
`explain` מאשר את זה: בעמוד רגיל `totalDocsExamined = 25` ו-`nReturned = 25`.

נמדד עם `explain('executionStats')` (הסקריפט: [scripts/perf-explain.js](scripts/perf-explain.js)) ועם זמני HTTP מקצה לקצה.
זמני HTTP הם חציון של 10 קריאות חמות, על מחשב פיתוח מקומי.

### Endpoint 1: ‏`GET /api/requests`
| שאילתה | תוכנית אחרי האופטימיזציה | keys / docs נבדקו | HTTP לפני → אחרי |
|--------|---------------|------------------|-----------------|
| עמוד 1, מיון ברירת מחדל | IXSCAN(ix_createdAt) → LIMIT | 25 / 25 | 83.6ms → **21.1ms** |
| סטטוס + עדיפות | IXSCAN(ix_status_priority_createdAt) | 25 / 25 | 16.6ms → 18.5ms |
| מיון לפי עדיפות | IXSCAN(ix_priority), לפני: **SORT בזיכרון ← COLLSCAN על 100K** | 25 / 25 | 95.3ms → **15.9ms** |
| סינון לפי ארגון | IXSCAN(ix_createdAt) + סינון | 206 / 206 | 17.4ms → 18.7ms |
| חיפוש טקסט (~10K התאמות) | IXSCAN(text) → **SORT בזיכרון** | 9,992 / 9,992 | 69.7ms → 71.7ms |
| עמוד 2,000 (skip 49,975) | IXSCAN → SKIP | 50,025 / 25 | 82.2ms → 59.5ms |

### Endpoint 2: ‏`GET /api/requests/summary`
| מצב | תוכנית | זמן |
|-----|--------|-----|
| cache hit | IMemoryCache | **2.9ms** |
| cache miss: לפני, 2 aggregations | COLLSCAN × 2 על 100K | חציון 418ms ב-DB |
| cache miss: אחרי, `$facet` אחד | COLLSCAN × 1 | חציון 278ms ב-DB, כ-300ms ב-HTTP |

### מה שופר בעקבות המדידה
1. **מיון לפי כל שדה ב-whitelist** עשה COLLSCAN ו-SORT בזיכרון על כל ה-collection. הוספתי אינדקס לכל שדה מיון, וכעת 25 keys נבדקים במקום 100K docs.
2. **דפדוף יציב:** שובר שוויון `_id` במיון ובאינדקסים (סעיף 5).
3. **`totalCount` בלי סינון:** `CountDocuments` עם filter ריק סורק את כל ה-collection (כ-65ms מתוך 83ms). כשאין סינון אני משתמש ב-`EstimatedDocumentCount` (מטא-דאטה, O(1)), וכך זמן העמוד הראשון ירד פי 4. עם סינון הספירה מדויקת ונתמכת באינדקס.
4. **הסיכום:** `$facet` אחד במקום שתי aggregations נפרדות. זה מעבר יחיד על ה-collection, והספירות עקביות זו עם זו.

### צווארי בקבוק שנותרו
- **ה-aggregation של הסיכום** סורק את כל ה-collection, O(N). אינדקס לא עוזר כאן: גם גרסה covered (IXSCAN בלי FETCH) נמדדה באותו זמן.
  ה-cache הוא שמנטרל את זה (2.9ms ב-hit). כשהנתונים יגדלו: מונים מנוהלים (collection של counters שמתעדכן באותה transaction של שינוי הסטטוס), כך שהסיכום הופך ל-O(1).
- **חיפוש טקסט:** Mongo לא יכול להשתמש באינדקס כדי למיין תוצאות `$text`, ולכן כל ההתאמות (כ-10K) נטענות וממוינות בזיכרון. עם עוד נתונים: Atlas Search, או הגבלת מספר התוצאות.
- **דפדוף עמוק:** `skip` עולה ב-O(skip), כ-50K keys בעמוד 2,000. השיפור הוא keyset pagination (`CreatedAt < last && _id < last`). האינדקסים כבר בנויים לזה.
- **ספירה מסוננת:** O(מספר ההתאמות), למשל 8,308 keys לסטטוס+עדיפות. זה מהיר, אבל גדל עם הנתונים.

---

## 7. Concurrency ו-Audit

**Optimistic Concurrency עם `Version`:** הלקוח שולח את הגרסה שראה (`expectedVersion`, שדה חובה: שדה חסר מחזיר 400).
העדכון ב-DB מותנה בגרסה ובסטטוס הנוכחי:
```
filter: _id = id  AND  Version = expectedVersion  AND  Status = currentStatus
update: Status = new, UpdatedAt = UtcNow, Version += 1
```
כל זה פעולה אטומית אחת (`FindOneAndUpdate`). אם המסמך השתנה בינתיים, לא מתבצע כלום ומוחזר **409**,
עם `currentVersion` ו-`currentStatus` כדי שהלקוח יוכל לרענן. בדיקה מקדימה ב-BL מחזירה 409 מהר, אבל האכיפה האמיתית היא התנאי ב-DB,
ולכן גם מרוץ בין הקריאה לכתיבה נתפס.

**Audit באותה transaction:** העדכון ורשומת ה-`statusAudit` נכתבים יחד ב-transaction (`WithTransactionAsync`). אין עדכון בלי audit ואין audit
בלי עדכון. `WithTransactionAsync` מבצע retry אוטומטי על WriteConflict בין transactions מקבילות, ובניסיון החוזר תנאי הגרסה כבר לא מתקיים,
כך שגם התנגשות ברמת ה-DB מסתיימת ב-409 ולא ב-500.

**אומת:** 10 עדכונים מקבילים עם אותה גרסה הניבו 200 אחד ו-9 תשובות 409, רשומת audit אחת, והגרסה עלתה ב-1 בלבד.
אותו תרחיש קיים גם כבדיקת integration.

**סדר הבדיקות:** מזהה ובקשה תקינים (400) ← הפנייה קיימת (404) ← הגרסה עדכנית (409) ← המעבר חוקי (400).
גרסה ישנה נבדקת לפני חוקיות המעבר, כי הלקוח צריך קודם לרענן ורק אז לבחור מעבר.

---

## 8. Bulk update

`PATCH /api/requests/bulk-status` מקבל עד 100 פניות. לכל אחת יש `expectedVersion` משלה, וסטטוס היעד משותף לכולן.

- **400 לכל הבקשה** כשהיא עצמה לא תקינה: רשימה ריקה, יותר מ-100 פניות, מזהים כפולים (גם כשההבדל רק באותיות גדולות/קטנות), מזהה לא תקין או גרסה חסרה
  (מוחזרת שגיאה לכל פריט, למשל `Items[1].ExpectedVersion`), או סטטוס יעד שאין אליו מעבר מאף סטטוס (`New`). במקרה כזה אף פנייה לא מעובדת.
- **Partial Success:** כל פנייה מתעדכנת ב-transaction משלה (עדכון ו-audit יחד), באותם כללים של עדכון בודד. כישלון של פנייה אחת לא מבטל אחרות.
  התשובה היא **200** עם דוח: `total`, `succeeded`, `failed`, ולכל פנייה `Updated` / `NotFound` / `Conflict` / `InvalidTransition`, יחד עם הגרסה והסטטוס העדכניים.
- המצב הנוכחי של כל הפניות נטען בשאילתת `$in` אחת, ולא ב-100 קריאות.
- ביטול באמצע (CancellationToken) עוצר את העיבוד. פניות שכבר עודכנו נשארות מעודכנות, וה-cache מתנקה גם במקרה הזה.

**אומת:** שתי בקשות bulk מקבילות על אותן 20 פניות הניבו בדיוק 20 עדכונים ו-20 רשומות audit בסך הכול, וכל פנייה עלתה בגרסה אחת בלבד.

---

## 9. Cache

**מה נשמר:** הסיכום (`/summary`). הוא סורק את כל ה-collection, נקרא בכל טעינת מסך, ומשתנה רק בשינוי סטטוס. יחס הקריאות לכתיבות גבוה והחישוב יקר.

- **Expiration:** 60 שניות (absolute), כרשת ביטחון לשינויים שלא עוברים דרך האפליקציה (עדכון ישיר ב-DB, או מופע שרת אחר).
- **Invalidation:** אחרי כל עדכון מוצלח, בודד, bulk (פעם אחת לכל ה-batch, ורק אם משהו עודכן) או seed. עדכון שנכשל לא מנקה את ה-cache.
- **מניעת נתון מיושן:** כל ניקוי מעלה מונה generation, והמונה הוא חלק מהמפתח. חישוב שהתחיל לפני עדכון והסתיים אחריו נשמר תחת מפתח ישן
  שאף אחד לא קורא, ולא "מחזיר לחיים" נתון מיושן ל-60 שניות. התרחיש מכוסה בבדיקת unit.

**Scale-out:** `IMemoryCache` שמור בזיכרון של כל מופע בנפרד. בריבוי מופעים, ניקוי במופע אחד לא מנקה את האחרים, ושם נתון עלול להיות מיושן עד 60 שניות.
לפרודקשן: `IDistributedCache` (Redis) עם מפתח משותף, או Redis pub/sub שמפיץ את הניקוי לכל המופעים. ה-BL תלוי רק בממשק `IRequestsSummaryCache`,
כך שאפשר להחליף מימוש בלי לשנות את ה-BL.

---

## 10. Frontend
- **Server-side מלא:** דפדוף, מיון, סינון וחיפוש נשלחים לשרת. בדפדפן נמצא רק העמוד הנוכחי.
- **חיפוש:** `debounceTime(300)` ו-`distinctUntilChanged`. כל שינוי בשאילתה (חיפוש, סינון, עמוד, מיון) עובר בזרם אחד עם `switchMap`,
  שמבטל את בקשת ה-HTTP הקודמת. כך תשובה ישנה לא יכולה לדרוס תשובה חדשה.
- **מצבי UI:** טעינה, ריק ושגיאה (עם "נסו שוב"), בטבלה, בסיכום ובדיאלוג ההיסטוריה.
- **409:** הודעה למשתמש, השורה מתרעננת לגרסה העדכנית, והסיכום מתרענן.
- **RTL ועברית:** תאריכים, datepicker ותוויות paginator.
- **Bundle:** Material נטען רק עם עמוד הפניות (lazy). ה-bundle הראשוני הוא 305KB.

---

## 11. החלטות מרכזיות וחלופות

**1. MongoDB ולא SQL Server.** נימוקים:
- ישות מרכזית אחת, בלי joins. מסמך אחד לכל פנייה הוא מבנה טבעי.
- ‏aggregation pipeline (`$facet`, ‏`$group`) מתאים לסיכומים, ואינדקס טקסט נותן חיפוש מובנה.
- ‏transactions זמינים ב-replica set (נדרשים לעדכון ול-audit יחד). replica set עם צומת אחד ב-Docker מספיק לפיתוח.
- היכרות קודמת עם MongoDB מהעבודה.

חלופה שנשקלה: *SQL Server*, עם `rowversion` שנותן Optimistic Concurrency מובנה (`WHERE RowVersion = @v`), ‏Full-Text Search, ו-transactions בלי תנאים מיוחדים.
זו חלופה טובה באותה מידה. המחיר שלה: שכבת ORM/migrations נוספת, ו-schema נוקשה יותר. המחיר של MongoDB: ‏`$text` מחפש מילים שלמות בלבד,
ואי אפשר למיין את תוצאות החיפוש באינדקס (סעיף 6).

**2. Optimistic Concurrency עם `Version`.** חלופות שנשקלו:
- *נעילה פסימית* (שדה lock או `findAndModify` עם בעלות): מונעת התנגשות מראש, אבל דורשת שחרור ו-timeout לנעילות יתומות, ומגבילה מקביליות. במסך עבודה שבו התנגשויות נדירות זה מחיר גבוה.
- *Last-write-wins*: פשוט, אבל זה בדיוק ה-Lost Update שהדרישה אוסרת.
- *ETag/If-Match ב-header*: שקול סמנטית. בחרתי שדה בגוף הבקשה כי אותו מבנה משמש גם ל-bulk, שבו לכל פנייה יש גרסה משלה.

**3. Bulk: ‏transaction לכל פנייה (Partial Success).** חלופות:
- *transaction אחת לכל ה-batch (all-or-nothing)*: עקבי יותר, אבל פנייה אחת עם גרסה ישנה מפילה את כל ה-100, וזה סותר את דרישת ה-Partial Success. גם transactions ארוכות מגדילות את הסיכוי ל-WriteConflict.
- *`BulkWrite` אחד בלי transaction*: מהיר יותר, אבל אז העדכון וה-audit לא אטומיים יחד, ואין דרך נקייה לדעת איזה פריט נכשל בגלל גרסה.

**4. Offset pagination (`skip`/`limit`) ולא keyset.** offset מאפשר לקפוץ לעמוד שרירותי ומתאים ל-`MatPaginator`. המחיר הוא O(skip) בעמודים עמוקים (סעיף 6).
keyset מהיר בכל עומק, אבל מאפשר רק "הבא/הקודם". האינדקסים כבר מוכנים למעבר.

**5. Modular monolith ולא microservices.** ישות אחת וצוות אחד. השכבות (Api/Bl/Dal/Dto) נותנות הפרדה, בלי מחיר של תקשורת בין שירותים.

---

## 12. מגבלות ושיפורים אפשריים
- **אין אימות והרשאות.** ה-API פתוח לכל מי שמגיע אליו, ו-`ChangedBy` הוא ערך שהלקוח שולח ולא מאומת. לפני חשיפה מחוץ לסביבת פיתוח:
  אימות (למשל JWT/OIDC), הרשאות לפי תפקיד, ו-`ChangedBy` מתוך זהות המשתמש המאומת. ה-endpoint של ה-seed חסום מחוץ ל-Development, ו-CORS מוגדר ל-`localhost:4200` בפיתוח בלבד.
- **חיפוש לפי מילים שלמות בלבד.** כך עובד `$text` (נתמך באינדקס, אבל בלי התאמה חלקית). השיפור: Atlas Search (autocomplete, fuzzy).
- **סינון ארגון ומשויך בהתאמה מדויקת**, כי אין endpoint שמחזיר את רשימת הערכים. השיפור: endpoint של `distinct` (נתמך באינדקס) ו-autocomplete ב-UI.
- **ה-cache שמור בכל מופע בנפרד** (סעיף 9), **ודפדוף עמוק** (סעיף 6).
- **בדיקות FE:** הממשק נבדק ידנית. לא נכתבו בדיקות אוטומטיות ל-Angular.

---

## 13. שימוש ב-AI
הקוד נכתב בעזרת Kiro (סוכן AI בתוך ה-IDE), על בסיס תוכנית העבודה ([WORK_PLAN.md](WORK_PLAN.md)) וכללי הסגנון ([CONVENTIONS.md](CONVENTIONS.md))
שהוכנו מראש. העבודה התקדמה Epic אחר Epic. ההחלטות הפתוחות (מקור `ChangedBy`, פורמט ה-404, סדר הבדיקות, זמן ה-expiration, שדרוג Node, ההיקף של סינון הארגון)
הוצגו לי והוכרעו על ידי, והממשק נבדק על ידי ידנית בדפדפן.

כל שלב אומת בפועל ולא רק נכתב: build נקי, הרצה מול Mongo אמיתי עם 100K רשומות, תרחישי מרוץ מקבילים, `explain` על השאילתות, ובסוף 54 בדיקות אוטומטיות.
חלק מהממצאים התגלו רק בזכות האימות הזה, ותוקנו:
- מיון אלפביתי של enums שנשמרו כמחרוזות.
- דפדוף לא יציב בגלל ערכים חוזרים במיון.
- `errors` שלא הוחזר ב-ValidationProblemDetails.
- ספירה מלאה של ה-collection בכל עמוד.
- ולידציית bulk שהסתירה שגיאות של פריטים.
- שגיאות model binding (‏`"Foo"` כסטטוס, JSON שבור) שהוחזרו בפורמט אחר ובאנגלית.
