using RequestsManagement.Api.ErrorHandling;
using RequestsManagement.Bl.Extensions;
using RequestsManagement.Dal.Extensions;

var builder = WebApplication.CreateBuilder(args);

// MVC controllers + Swagger
builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        // Enums are sent/received as string names (a clean contract for the FE), e.g. "Completed"/"High".
        options.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Cross-cutting + layers (see CONVENTIONS A4)
builder.Services.AddGlobalErrorHandler();        // IExceptionHandler + ProblemDetails
builder.Services.AddDal(builder.Configuration);  // Mongo factory + context + settings + indexes + seeder
builder.Services.AddBl();                        // validators + cache

// Ensure indexes on startup
builder.Services.AddHostedService<RequestsManagement.Api.Startup.IndexInitializationHostedService>();

// CORS for the FE in development only (Angular runs on a different origin). The origins are configured
// in appsettings.Development.json; no policy in other environments — there the FE is served from the same origin.
const string DevFrontendCorsPolicy = "DevFrontend";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
    options.AddPolicy(DevFrontendCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .WithMethods("GET", "POST", "PATCH")
        .WithHeaders("Content-Type")));

var app = builder.Build();

// The global error handler must be early in the pipeline
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

/// <summary>Exposes Program to integration tests (WebApplicationFactory&lt;Program&gt;).</summary>
public partial class Program;
