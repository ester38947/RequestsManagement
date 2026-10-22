# כללי סגנון ואדריכלות — Reference לבנייה

> מסמך זה מתמצת את סגנון הקוד של פרויקטי העבודה הקיימים (BE: `EnvImpactReviewsBE`, FE: `EnvImpactReviewsFE`)
> כפי שנחקרו לעומק. מטרתו: לאפשר בניית הפרויקט החדש **באותו סגנון בדיוק** גם במחשב שבו אין גישה לפרויקטי המקור.
> הפרויקט החדש הוא פשוט יותר (modular monolith, ישות אחת), אז מאמצים את **הסגנון והקונבנציות** — לא את המורכבות (ללא microservices, ללא GCP Secret Manager, ללא inter-service HTTP, ללא Native Federation).

---

## חלק א׳ — Backend (.NET 9 + MongoDB)

### A1. חלוקת שכבות (4 פרויקטים)
אותה חלוקה כמו בעבודה, בשמות מותאמים לפרויקט:
```
RequestsManagement.Api   (Microsoft.NET.Sdk.Web)  → Controllers, Program.cs, Middleware, appsettings, Dockerfile
RequestsManagement.Bl    (classlib)               → Interfaces/, Services/, Validators/, Caching/, Extensions/ServiceCollectionExtensions.cs (AddBl)
RequestsManagement.Dal   (classlib)               → Context/, Settings/, Models/, Mapping/, Repositories/, Interfaces/, Indexes/, Seed/, Extensions/ServiceCollectionExtensions.cs (AddDal)
RequestsManagement.Dto   (classlib)               → feature folders, ApiResponseDto<T>, Enums/, Paging/
```
כיווני תלות (חד‑כיווניים, חוץ→פנים):
- **Api** → Bl + Dto
- **Bl** → Dal + Dto
- **Dal** → Dto
- **Dto** → (ללא תלות)

### A2. TargetFramework ו‑NuGet
- `net9.0`, `<Nullable>enable</Nullable>`, `<ImplicitUsings>enable</ImplicitUsings>` בכל הפרויקטים.
- Dal: `MongoDB.Driver` 3.8.0, `Microsoft.Extensions.DependencyInjection.Abstractions` 9.0.0.
- Api: `Swashbuckle.AspNetCore` (Swagger), `Microsoft.AspNetCore.OpenApi` 9.0.x.
- Bl: `FluentValidation`, `FluentValidation.DependencyInjectionExtensions`.
- Caching: `Microsoft.Extensions.Caching.Memory`.

### A3. חיבור MongoDB (ללא GCP — זה השינוי המרכזי מהעבודה)
בעבודה ה‑connection string נמשך מ‑Google Cloud Secret Manager. **בפרויקט החדש: connection string רגיל מ‑`appsettings.json` / env var.** שאר התבנית זהה:
- `MongoConnectionFactory` — עוטף `MongoClient`; `MongoClientSettings.FromConnectionString(cs)` + `ServerApi = new ServerApi(ServerApiVersion.V1)`. חשוף `GetDatabase(name)`. נרשם **Singleton**.
- `AppMongoDbContext` — מוזרק עם ה‑factory ה‑Singleton, קורא `GetDatabase(dbName)`, חושף collections טיפוסיים דרך properties `GetCollection<T>(name)`. נרשם **Singleton**.
- שמות DB/collections כקבועים ב‑`Settings/MongoDbSettings.cs` (`DatabaseName`, nested `Collections`).
- `appsettings.json` מכיל `Mongo:ConnectionString` + `Mongo:DatabaseName`.

דוגמת factory (לשחזור):
```csharp
public sealed class MongoConnectionFactory
{
    private readonly IMongoClient _client;
    public MongoConnectionFactory(IOptions<MongoDbSettings> options)
    {
        var settings = MongoClientSettings.FromConnectionString(options.Value.ConnectionString);
        settings.ServerApi = new ServerApi(ServerApiVersion.V1);
        _client = new MongoClient(settings);
    }
    public IMongoClient Client => _client;
    public IMongoDatabase GetDatabase(string name) => _client.GetDatabase(name);
}
```

### A4. Dependency Injection + Lifetimes
הרכבה ב‑`Program.cs` דרך extension methods לפי שכבה:
```csharp
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddGlobalErrorHandler();        // IExceptionHandler + ProblemDetails
builder.Services.AddDal(builder.Configuration);  // Mongo factory+context+repositories+indexes
builder.Services.AddBl();                         // services + validators + cache
```
Lifetimes (קבוע):
- `MongoConnectionFactory`: **Singleton**
- `AppMongoDbContext`: **Singleton**
- Repositories: **Scoped**
- BL Services: **Scoped**
- Validators: `AddValidatorsFromAssemblyContaining<...>()` (assembly scan)
- `IMemoryCache`: Singleton (ברירת מחדל של `AddMemoryCache`)

### A5. Primary constructors בכל מקום (C# 12)
```csharp
public sealed class RequestRepository(AppMongoDbContext context) : IRequestRepository
{
    private readonly IMongoCollection<RequestModel> _col = context.Requests;
    // ...
}
public sealed class RequestService(IRequestRepository repo, IValidator<RequestSearchRequest> validator) : IRequestService
{
    // ...
}
```

### A6. Repository — Mongo fluent API
- פילטרים: `Builders<T>.Filter.Eq/Gte/Lte/Regex/In` + `.And(...)`.
- עדכונים: `Builders<T>.Update.Set(...).Inc(...)` עם `UpdateOneAsync`; בדיקת הצלחה `result.ModifiedCount > 0`.
- מיון: `Builders<T>.Sort.Ascending/Descending`.
- **Soft-delete**: שדה `IsActive`; מחיקה מסמנת `IsActive=false`; קריאות מסננות `IsActive==true`. (בפרויקט החדש — רלוונטי אם נדרש; לפניות ייתכן שלא נמחק כלל.)
- Timestamps תמיד **UTC** (`DateTime.UtcNow`).
- **כל פעולת I/O מקבלת `CancellationToken ct`** ומעבירה ל‑driver (`...Async(..., cancellationToken: ct)`). (בעבודה זה היה חלקי — בפרויקט החדש זו דרישת מבדק, אז עקבי בכל מקום.)

### A7. BaseEntity (מודל בסיס ל‑Mongo)
```csharp
public abstract class BaseEntity
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = default!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
```
עבור הפנייה מוסיפים `Version` (int) ל‑Optimistic Concurrency.

### A8. Mapping — static mappers ידניים (ללא AutoMapper)
`Dal/Mapping/RequestMapper.cs` — מחלקה סטטית עם `ToListItemDto`, `ToDetailsDto`, `ToModel`, ו‑helpers פרטיים לאובייקטים מקוננים. שקיפות מלאה, קל להסביר.

### A9. DTOs ו‑ApiResponseDto<T>
כל תגובת API עטופה ב‑envelope אחיד:
```csharp
public sealed class ApiResponseDto<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public string? Error { get; init; }
    public static ApiResponseDto<T> Ok(T data) => new() { Success = true, Data = data };
    public static ApiResponseDto<T> Fail(string error) => new() { Success = false, Error = error };
}
```
DTOs מקובצים לפי feature. **אף פעם לא מחזירים ישות DB ישירות** — תמיד DTO.

### A10. Controllers
- `[ApiController]` + route kebab-case: `[Route("api/requests")]`.
- Primary ctor מזריק את ה‑BL interface.
- כל action: `async Task<ActionResult<ApiResponseDto<T>>>`, מקבל `CancellationToken ct`.
- רשימה: `[HttpGet]` עם `[FromQuery] RequestSearchRequest`.
- פרט: `[HttpGet("{id}")]`.
- עדכון חלקי: `[HttpPatch("{id}/status")]`.
- Bulk: `[HttpPatch("bulk-status")]`.
- לא קיים → `NotFound(ApiResponseDto<T>.Fail("..."))` עם הודעת משתמש **בעברית**.
- Controllers **לא** תופסים exceptions — מועבר ל‑global handler.

### A11. Validation — FluentValidation
- Validators ב‑`Bl/Validators/`, יורשים `AbstractValidator<T>`, `.Cascade(CascadeMode.Stop)`.
- `NotEmpty/MaximumLength/InclusiveBetween/Must`, `.When(...)` מותנה, `.WithName("<תווית עברית>")`.
- קבועים מרוכזים ב‑`ValidationMessages`/`ValidationConstants`.
- מופעל ב‑BL: `await validator.ValidateAndThrowAsync(request, ct)`.

### A12. Global Error Handling — IExceptionHandler (.NET 8+)
- `AddGlobalErrorHandler()` = `AddExceptionHandler<GlobalExceptionHandler>()` + `AddProblemDetails()`.
- מיפוי: `ValidationException`/`ArgumentException`→400, `KeyNotFoundException`→404, `UnauthorizedAccessException`→401, אחרת→500.
- `ValidationException` → `ValidationProblemDetails` (קיבוץ שגיאות לפי property, כותרת עברית "שגיאות ולידציה").
- אחרת → `ProblemDetails` (RFC 7807) עם `Detail`.
- לוג Warning ל‑4xx, Error ל‑5xx.

### A13. Logging
בעבודה: `Sviva.Xrm.FrameworkCore.Log` (NuGet פנימי) — **לא זמין מחוץ לארגון.** בפרויקט החדש משתמשים ב‑`Microsoft.Extensions.Logging` הסטנדרטי (`ILogger<T>`), באותה פילוסופיה:
- לוג בנקודות משמעותיות (כניסת בקשה, קונפליקט concurrency, כשל bulk, שגיאות).
- **לא לחשוף מידע רגיש** בלוגים.
- מבנה הודעה עקבי + structured logging (`logger.LogWarning("Concurrency conflict on {RequestId}", id)`).

### A14. Concurrency (חדש — ליבת המבדק)
- שדה `Version` (int) על המודל.
- עדכון: `filter = Eq(Id) & Eq(Version, expectedVersion)`; `update = Set(status).Set(UpdatedAt).Inc(Version,1)`.
- `UpdateOneAsync` → אם `ModifiedCount==0` ו‑המסמך קיים → **409 Conflict** (עדכון מתחרה).
- Bulk + audit → **transaction** (`IClientSessionHandle`, דורש replica set).

---

## חלק ב׳ — Frontend (Angular)

### B1. גרסה וסגנון
- Angular **21** (או הגרסה היציבה הזמינה), standalone components, signals, `inject()`, control flow חדש (`@if`/`@for`/`@empty`).
- RxJS ~7.8, TypeScript ~5.9.
- **ללא NgModules, ללא Native Federation** (זה over-engineering לאפליקציה יחידה) — אפליקציה אחת רגילה.
- **ללא NgRx** — state דרך signals בתוך services `providedIn: 'root'`.

### B2. UI Library — Angular Material (סטנדרטי)
בעבודה: IGDS (ממשלתי, לא זמין חיצונית). בפרויקט החדש: **Angular Material** — `mat-table` + `MatPaginator` + `MatSort` מתאימים מושלם ל‑server-side paging. שומרים **RTL + עברית** (`dir="rtl"`, `@angular/cdk/bidi`).

### B3. שמות קבצים
בלי סיומת `.component`/`.service` בשם הקובץ: `request-list.ts`, `request-list.html`, `request-list.scss` (שם המחלקה כן `RequestListComponent`). Root: `app.ts` → `class App`.

### B4. HTTP — BaseHttpService + API_BASE_URL token
```typescript
export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL');

export abstract class BaseHttpService {
  protected readonly http = inject(HttpClient);
  protected readonly apiBaseUrl = inject(API_BASE_URL);
  protected abstract readonly servicePath: string;
  protected get baseUrl(): string { return `${this.apiBaseUrl}/api/${this.servicePath}`; }
  protected get<T>(path: string, params?: Record<string,string>): Observable<T> { /* HttpParams */ }
  protected post<T>(path: string, body: unknown): Observable<T> { /* ... */ }
  protected patch<T>(path: string, body: unknown): Observable<T> { /* ... */ }
}
```
> הערה: בעבודה ה‑base path היה `/gateway/api/` (כי יש API gateway). בפרויקט החדש (ללא gateway) → `/api/`.

### B5. Services — signals + envelope unwrap
- `providedIn: 'root'`, מחזיקים state ב‑signals (`_current = signal(...)`, חשוף `.asReadonly()`).
- כל קריאה מחזירה `ApiResponse<T>` ועושים `.pipe(map(res => res.data!))` כך שהרכיב מקבל טיפוס דומיין נקי.
- envelope ב‑FE תואם ל‑BE: `interface ApiResponse<T> { success: boolean; data: T | null; error: string | null; }`.

### B6. RxJS לחיפוש (חדש — דרישת מבדק)
בעבודה החיפוש היה client-side דרך `computed`. **בפרויקט החדש server-side:**
- `searchControl.valueChanges.pipe(debounceTime(300), distinctUntilChanged(), switchMap(term => service.search(...)))` — `switchMap` **מבטל בקשה קודמת** כשהחיפוש משתנה מהר (דרישת מבדק מפורשת).
- `catchError` להחזרת מצב ריק + הצגת שגיאה; `finalize`/`tap` לכיבוי loading.

### B7. מצבי UI — Loading / Empty / Error
תבנית קנונית:
```html
@if (loading()) { <app-skeleton /> }
@else if (error()) { <div class="error-state">{{ error() }}</div> }
@else {
  @for (item of items(); track item.id) { <!-- row --> }
  @empty { <p class="empty-state">לא נמצאו פניות</p> }
}
```
`loading` כ‑`signal(true)`, מתכבה ב‑subscribe/finalize.

### B8. הפרדת אחריות (smart/dumb)
- **Smart containers** (feature components): מזריקים services, מחזיקים signals, מתזמרים נתונים.
- **Dumb/presentational** (ל‑`shared/` או components פנימיים): `input()`/`output()` בלבד, בלי גישה ל‑service.

### B9. Routing
- `provideRouter(routes)` ב‑`app.config.ts`.
- `provideHttpClient(withInterceptors([...]))`; interceptor פונקציונלי אם צריך (למשל header זהות בפיתוח).
- `{ provide: API_BASE_URL, useValue: environment.apiBaseUrl }`.
- Lazy loading לנתיבים דרך `loadComponent`.

### B10. סביבות וקונפיג
- `src/environments/environment.ts` עם `apiBaseUrl` (dev: `http://localhost:<port>`).
- `fileReplacements` ל‑prod ב‑angular.json.

### B11. Forms
- Reactive Forms.
- helpers מותנים בסגנון `form-utils` (אם נדרש): `applyConditionalRequired/State/Reset` עם `takeUntilDestroyed(destroyRef)`.

### B12. עיצוב ו‑Lint
- SCSS; theme של Material (אפשר theme בהיר סטנדרטי) + RTL.
- **Prettier**: `printWidth: 100`, `singleQuote: true`, override ל‑`*.html` עם parser `angular`.
- **.editorconfig**: UTF-8, 2 רווחים, final newline, trim trailing whitespace.

---

## חלק ג׳ — מה *לא* מעתיקים מהעבודה (כדי להימנע מ‑Over-Engineering)
| בעבודה | בפרויקט החדש | סיבה |
|--------|--------------|------|
| Microservices (11 שירותים) | Modular monolith יחיד | ישות אחת; המבדק מזהיר מ‑over-engineering |
| GCP Secret Manager | connection string ב‑config/env | אין GCP; הגשה פשוטה לבודק |
| Inter-service HTTP + Polly + GenericApiClient | — | אין שירותים אחרים |
| Native Federation (shell+remotes) | אפליקציית Angular יחידה | אין צורך בריבוי אפליקציות |
| IGDS (UI ממשלתי) | Angular Material | זמין לכל בודק |
| `Sviva.Xrm.FrameworkCore.Log` | `Microsoft.Extensions.Logging` | NuGet פנימי לא זמין חיצונית |

**מה כן שומרים:** את כל *הסגנון* — layering, primary constructors, static mappers, FluentValidation, ApiResponseDto, IExceptionHandler, signals+BaseHttpService, שמות קבצים, UTC timestamps, soft-delete-כשרלוונטי, Swagger.
