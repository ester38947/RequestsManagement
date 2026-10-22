using RequestsManagement.Api.ErrorHandling;
using RequestsManagement.Bl.Extensions;
using RequestsManagement.Dal.Extensions;

var builder = WebApplication.CreateBuilder(args);

// MVC controllers + Swagger
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // enums נשלחים/מתקבלים כשמות מחרוזת (חוזה נקי ל-FE), למשל "Completed"/"High".
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cross-cutting + layers (ראה CONVENTIONS A4)
builder.Services.AddGlobalErrorHandler();        // IExceptionHandler + ProblemDetails
builder.Services.AddDal(builder.Configuration);  // Mongo factory + context + settings + indexes + seeder
builder.Services.AddBl();                        // validators + cache

// ensure-on-startup לאינדקסים (Task 1.2)
builder.Services.AddHostedService<RequestsManagement.Api.Startup.IndexInitializationHostedService>();

// CORS ל-FE בפיתוח בלבד (Angular על origin אחר). ה-origins מוגדרים ב-appsettings.Development.json;
// אין policy בסביבות אחרות — שם ה-FE מוגש מאותו origin.
const string DevFrontendCorsPolicy = "DevFrontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(DevFrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST", "PATCH")
        .WithHeaders("Content-Type")));

var app = builder.Build();

// global error handler חייב להיות מוקדם ב-pipeline
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseCors(DevFrontendCorsPolicy);
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();

app.Run();

/// <summary>חשיפת Program לבדיקות integration (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
