# תוכנית עבודה — מערכת ניהול פניות/בקשות (Requests Management)

> מסמך זה הוא התרגום של אפיון משימת הבית לתוכנית עבודה מעשית: חלוקה ל‑Features ו‑Tasks,
> תלויות, סדר ביצוע, הערכת מאמץ, ומיפוי מלא בין כל דרישת מבדק למשימה שמכסה אותה.
> הוא עצמו מהווה דרישה מנוקדת (סעיף 9 — "תכנון עבודה וחלוקה למשימות").

---

## 1. סקירה והחלטות יסוד

| נושא | החלטה | נימוק קצר |
|------|-------|-----------|
| ישות מרכזית | `Request` (פנייה), collection בשם `requests` | תואם לאפיון "פניות/בקשות". |
| Backend | .NET 9, **modular monolith** בשכבות API/BL/DAL/DTO | סגנון הקוד הקיים; service-ready אך ללא over-engineering של microservices. |
| Database | MongoDB (Driver 3.8.0), **replica set** | תמיכה ב‑transactions (נדרש ל‑bulk + concurrency). Mongo שהמפתח מכיר מהעבודה. |
| Concurrency | Optimistic Concurrency דרך שדה `Version` | אכיפה בצד השרת/DB (לא רק UI) — דרישת ליבה. |
| Frontend | Angular 21, אפליקציה יחידה standalone + signals | סגנון הקוד הקיים ללא Native Federation (over-engineering לישות אחת). |
| UI Library | **Angular Material** (סטנדרטי) | מתקין מיידית אצל הבודק; table + paginator + sort מובנים שתואמים server-side paging. RTL + עברית נשמרים. |
| Mapping | Static mappers ידניים (בלי AutoMapper) | סגנון הקוד הקיים; שקיפות מלאה. |
| Validation | FluentValidation | סגנון הקוד הקיים. |
| Error handling | `IExceptionHandler` גלובלי + ProblemDetails (RFC 7807) | סגנון הקוד הקיים. |
| Cache | `IMemoryCache` עם invalidation ותיאור התאמה ל‑scale-out | דרישת סעיף 7. |

### מבנה הפנייה (מהאפיון)
`Id`, `Title`, `OrganizationName`, `Status` (New/InProgress/Waiting/Completed),
`Priority` (Low/Medium/High), `AssignedTo`, `CreatedAt`, `UpdatedAt`, `Version` (RowVersion לזיהוי עדכון מתחרה).

---

## 2. מבנה הפתרון (High-level)

```
RequestsManagement/
├─ docker-compose.yml                # MongoDB replica-set (הרצה בלחיצה)
├─ README.md                         # תיעוד מלא (סעיף 11)
├─ WORK_PLAN.md                      # מסמך זה (סעיף 9)
├─ RequestsManagement.BE\
│  ├─ RequestsManagement.sln
│  ├─ src\
│  │  ├─ RequestsManagement.Api\     # Controllers, Program.cs, Middleware, appsettings
│  │  ├─ RequestsManagement.Bl\      # Services, Interfaces, Validators, Extensions, Caching
│  │  ├─ RequestsManagement.Dal\     # Context, Settings, Models, Mapping, Repositories, Indexes, Seed
│  │  └─ RequestsManagement.Dto\     # Request/Response DTOs, ApiResponseDto<T>, Enums, Paging
│  └─ tests\
│     ├─ RequestsManagement.UnitTests\
│     └─ RequestsManagement.IntegrationTests\
└─ RequestsManagement.FE\
   └─ Angular workspace (single app) — components, services, models, interceptors
```

---

## 3. חלוקה ל‑Features ו‑Tasks

הערכת מאמץ ביחידות יחסיות (S≈חצי יום, M≈יום, L≈יומיים). האותיות בעמודת "מכסה" מפנות לדרישות המבדק.

### Epic 0 — תשתית והיערכות להרצה
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 0.1 | Scaffold פתרון BE (4 פרויקטים + 2 טסטים + sln) | solution בונה ריק ועובר `dotnet build` | S | — | 10 |
| 0.2 | `docker-compose.yml` ל‑MongoDB replica-set + healthcheck | `docker compose up` מעלה Mongo מוכן ל‑transactions | S | — | 6,11 |
| 0.3 | חיבור Mongo: `MongoConnectionFactory`+`Context`+`Settings` (connection string מ‑config/env, ללא GCP) | אפליקציה מתחברת ל‑Mongo | S | 0.1,0.2 | 10,11 |
| 0.4 | תשתית Logging + `ApiResponseDto<T>` + global `IExceptionHandler` | שגיאות מוחזרות כ‑ProblemDetails; לוגים בנקודות מפתח | S | 0.1 | 10 |

### Epic 1 — מודל נתונים, אינדקסים ו‑Seed
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 1.1 | מודל `Request` (BSON) + enums `Status`/`Priority` + מכונת מצבים למעברים חוקיים | מסמך Mongo תקין | S | 0.3 | DB, 2 |
| 1.2 | הגדרת אינדקסים (ensure-on-startup) התומכים בסינון/חיפוש/מיון | אינדקסים נוצרים אוטומטית; מתועדים ב‑README | M | 1.1 | 1,6 |
| 1.3 | Seed מחדש של ≥100,000 רשומות (bulk insert, חוזר על עצמו) דרך endpoint/CLI | הרצה יוצרת 100K רשומות מגוונות | M | 1.1 | DB |
| 1.4 | מודל `StatusAuditEntry` (היסטוריית שינויי סטטוס) | collection audit | S | 1.1 | 3 |

### Epic 2 — Query API (שליפה)
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 2.1 | `GET /requests` עם **server-side pagination** (skip/limit + totalCount) | דף נתונים + מטא, ללא טעינת הכל לזיכרון | M | 1.2 | 1 |
| 2.2 | סינון משולב: Status, Priority, OrganizationName, AssignedTo, טווח CreatedAt | פילטרים משתלבים ב‑AND | M | 2.1 | 1 |
| 2.3 | חיפוש טקסטואלי ב‑Title + OrganizationName | חיפוש נתמך‑אינדקס | S | 2.1 | 1 |
| 2.4 | מיון לפי ≥3 שדות (CreatedAt/Priority/Status...) Asc/Desc | מיון דינמי מוגבל ל‑whitelist | S | 2.1 | 1 |
| 2.5 | Validation לפרמטרים + הגבלת PageSize (מקס' מוגדר) | 400 ברור על קלט לא תקין | S | 2.1 | 1,10 |
| 2.6 | 2 Aggregations (למשל ספירה לפי Status, ולפי Priority/יומי) | endpoint summary + נתונים | M | 1.2 | 1,DB |
| 2.7 | `CancellationToken` בכל פעולות ה‑I/O + החזרת DTOs בלבד | ביטול זורם ל‑Mongo | S | 2.1 | 1,10 |

### Epic 3 — עדכון סטטוס, Concurrency ו‑Audit
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 3.1 | `PATCH /requests/{id}/status` עם בדיקת מעבר חוקי + 404/400 | מעבר לא חוקי → 400 ברור; לא קיים → 404 | M | 1.1 | 2 |
| 3.2 | Optimistic Concurrency: תנאי `Version` ב‑update; חוסר התאמה → 409 | עדכון מתחרה אחד מצליח, השני 409; מניעת Lost Update | M | 3.1 | 2 |
| 3.3 | עדכון עקבי של `UpdatedAt` + `Version++` באותה פעולה אטומית | שדות עקביים תמיד | S | 3.2 | 2 |
| 3.4 | כתיבת Audit (RequestId, Previous/NewStatus, ChangedAt, ChangedBy) בכל שינוי | רשומת audit לכל מעבר | S | 1.4,3.1 | 3 |
| 3.5 | `GET /requests/{id}/history` | היסטוריה ממוינת | S | 3.4 | 3 |

### Epic 4 — Bulk update
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 4.1 | `PATCH /requests/bulk-status` עד 100 פניות, **Partial Success** + דוח per-item | כל פנייה: Success/NotFound/Conflict | M | 3.2,3.4 | 4 |
| 4.2 | Validation: מקס' 100, רשימה לא ריקה, מעבר חוקי | 400 ברור | S | 4.1 | 4 |

### Epic 5 — Cache
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 5.1 | Cache ל‑Aggregation המסכם (`IMemoryCache` + expiration) | קריאות חוזרות מהמטמון | S | 2.6 | 7 |
| 5.2 | Invalidation של הסיכום בעדכון סטטוס/bulk; תיאור התאמת scale-out | אין מידע מיושן אחרי עדכון | S | 5.1,3.x | 7 |

### Epic 6 — Frontend (Angular)
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 6.1 | Scaffold Angular + Material + RTL/עברית + `API_BASE_URL` + `BaseHttpService` | אפליקציה עולה | S | — | 5 |
| 6.2 | `RequestsService` (server-side params) + envelope unwrap | שירות טיפוסי | S | 6.1,2.x | 5 |
| 6.3 | טבלה: paginator + sort + עמודות, server-side | רשימה מדפדפת מהשרת | M | 6.2 | 5 |
| 6.4 | חיפוש עם **debounceTime** + **switchMap** (ביטול בקשה קודמת) | הקלדה מהירה לא מרעישה בקשות | M | 6.3 | 5 |
| 6.5 | פאנל סינון (Status/Priority/Org/AssignedTo/טווח תאריכים) | סינון server-side | M | 6.3 | 5 |
| 6.6 | Loading / Empty / Error states | שלושת המצבים גלויים | S | 6.3 | 5 |
| 6.7 | עדכון סטטוס + טיפול ב‑409 Conflict (רענון/הודעה) | משתמש מקבל משוב קונפליקט | M | 6.3,3.2 | 5 |
| 6.8 | תצוגת Aggregations (summary cards/גרף פשוט) | מוצג סיכום | S | 6.2,2.6 | 5 |
| 6.9 | תצוגת היסטוריית שינויים | דיאלוג/פאנל היסטוריה | S | 6.2,3.5 | 5 |
| 6.10 | הפרדת אחריות Components/Services | smart/dumb ברור | S | 6.3 | 5,10 |

### Epic 7 — ביצועים, בדיקות ותיעוד
| # | Task | תוצר מצופה | מאמץ | תלוי ב־ | מכסה |
|---|------|-----------|------|---------|------|
| 7.1 | מדידת ביצועים ל‑2 endpoints (explain/executionStats על 100K) + אינדקסים + bottleneck | מסמך ביצועים ב‑README | M | 2.x,1.3 | 6 |
| 7.2 | ≥4 בדיקות: שליפה/סינון תקין, קלט לא תקין, concurrency conflict, Integration (API+DB) | טסטים עוברים | L | 3.2,4.1,2.x | 8 |
| 7.3 | README מלא: הרצה, טכנולוגיות/גרסאות, מבנה, DB+אינדקסים, concurrency, bulk, cache, 2 החלטות+חלופות, מגבלה+שיפור, שימוש ב‑AI | תיעוד שלם | M | כל השאר | 11 |

---

## 4. סדר ביצוע מומלץ (Critical Path)

```
0.1 → 0.2 → 0.3 → 0.4
      → 1.1 → 1.2 → 1.3 (seed)
                  → 2.1 → 2.2/2.3/2.4/2.5/2.7 → 2.6
      1.1 → 1.4
3.1 → 3.2 → 3.3 → 3.4 → 3.5        (אחרי 2.1 כדי לנצל repository)
4.1 → 4.2                          (אחרי 3.2+3.4)
5.1 → 5.2                          (אחרי 2.6)
FE 6.1 → 6.2 → 6.3 → {6.4,6.5,6.6,6.7,6.8,6.9} → 6.10   (במקביל ל‑BE אחרי שה‑API חוזה יציב)
7.1, 7.2, 7.3                      (לאורך הדרך; מסכמים בסוף)
```

**אבני דרך:**
- **M1 — Query API חי על 100K** (Epics 0,1,2): אפשר להדגים שליפה/סינון/מיון/דפדוף + aggregations.
- **M2 — Concurrency + Audit + Bulk** (Epics 3,4): ליבת ה‑write.
- **M3 — Cache + FE מלא** (Epics 5,6).
- **M4 — ביצועים, טסטים, תיעוד** (Epic 7) — מוכן להגשה.

---

## 5. מיפוי דרישות מבדק → משימות (כיסוי מלא)

| דרישת מבדק | משימות מכסות |
|------------|--------------|
| 1. שליפה (paging/filter/search/sort/aggregations/validation/CT/DTOs) | 2.1–2.7 |
| 2. עדכון סטטוס ותחרותיות (404/400/409/UpdatedAt+Version/Lost Update) | 3.1–3.3 |
| 3. היסטוריית שינויים (Audit + endpoint) | 1.4, 3.4, 3.5 |
| 4. Bulk (עד 100, Partial Success מתועד) | 4.1, 4.2 |
| 5. Angular (server-side, debounce, ביטול בקשות, מצבי UI, 409, aggregations, history, הפרדה) | 6.1–6.10 |
| 6. ביצועים (explain, אינדקסים, bottleneck, בלי טעינה מלאה) | 1.2, 7.1 |
| 7. Cache (expiration, invalidation, נימוק, scale-out) | 5.1, 5.2 |
| 8. בדיקות (תקין, validation, concurrency, integration) | 7.2 |
| 9. תכנון עבודה | מסמך זה |
| 10. איכות קוד (DI/lifetimes/async/DTOs/error/validation/logging) | לרוחב כל ה‑Epics |
| 11. תיעוד | 7.3 |

---

## 6. סיכונים והיערכות
- **Transactions דורשים replica set** — מטופל ב‑docker-compose (single-node RS) ונתמך גם ב‑Atlas.
- **זמן seed של 100K** — יבוצע ב‑bulk `InsertMany` עם batching; יתועד זמן.
- **זמינות Docker אצל הבודק** — README יספק גם מסלול Atlas חלופי.
- **מגבלת scale-out ל‑IMemoryCache** — יתועד (ממטמון per-instance; לפרודקשן Redis).

---

## 7. סטטוס ביצוע וסטיות מהתכנון

כל ה-Epics (0–7) בוצעו. כל אבן דרך אומתה בהרצה מול MongoDB עם 100K רשומות לפני שעברתי לבאה.
הטבלה מתעדת מה השתנה מהתכנון ולמה. רוב השינויים נבעו ממדידה או מבדיקה, ולא מתכנון מראש.

| נושא | בתכנון | בפועל | סיבה |
|------|--------|-------|------|
| אחסון enums ב-DB | מחרוזות (החלטה ב-1.1, לקריאוּת ב-DB) | Int32. ה-API עדיין מחזיר שמות | על מחרוזות, מיון לפי עדיפות יצא אלפביתי (High < Low < Medium). נמצא בבדיקת 2.4 |
| אינדקסים (1.2) | 5 אינדקסים | 9 אינדקסים + אינדקס audit. כל אינדקס מיון מסתיים ב-`_id` | `explain` (7.1) הראה COLLSCAN + SORT במיון לפי שדות בלי אינדקס. בנוסף, דפדוף לא יציב: 578 כפילויות ב-40 עמודים |
| ספירה בלי סינון | `CountDocuments` | `EstimatedDocumentCount` כשאין סינון | נמדד: הספירה לקחה 65ms מתוך 83ms של העמוד הראשון |
| aggregations (2.6) | שתי aggregations | `$facet` אחד | מעבר יחיד על ה-collection (418ms → 278ms), והספירות עקביות זו עם זו |
| endpoints | רשימה, סיכום, עדכון, bulk, היסטוריה | + `GET /requests/{id}` | נדרש ל-FE לרענון השורה אחרי 409 (6.7) |
| Seed (1.3) | הוספה חוזרת | + `reset=true` (מחיקה ויצירה מחדש) | "יצירה חוזרת של נתוני הבדיקה" צריכה לתת בדיוק 100K, ולא 200K בהרצה השנייה |
| שגיאות קלט | FluentValidation | + תשובת model binding אחידה | `"Foo"` כסטטוס, או JSON שבור, החזירו את ה-400 של ASP.NET באנגלית, לפני הוולידציה שלנו |
| CORS | לא תוכנן | Development בלבד, ‏`localhost:4200` | ה-FE רץ על origin אחר בפיתוח |
| סביבת FE (6.1) | Angular 21 | Angular 21, עם שדרוג Node 20.11 ל-24 LTS | Angular 21 דורש Node ‏`^20.19 / ^22.12 / ^24` |
| בדיקות (7.2) | לפחות 4 | 40 unit + 14 integration | כיסוי של כל תרחיש מרכזי, כולל race ב-cache וב-concurrency |

**החלטות שהתקבלו במהלך העבודה:** ‏`ChangedBy` נשלח בגוף הבקשה (header לא תומך בעברית מהדפדפן). ‏404 בקריאה מוחזר כ-envelope ובכתיבה כ-ProblemDetails.
גרסה ישנה נבדקת לפני חוקיות המעבר. ‏expiration של 60 שניות ל-cache. הסינון לפי ארגון ומשויך נשאר בהתאמה מדויקת.
