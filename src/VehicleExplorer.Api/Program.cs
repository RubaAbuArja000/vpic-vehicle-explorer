using Microsoft.AspNetCore.Diagnostics;
using VehicleExplorer.Api.Services;
using VehicleExplorer.Api.Vpic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddMemoryCache();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services.Configure<CacheOptions>(builder.Configuration.GetSection(CacheOptions.Section));

var vpicBaseUrl = builder.Configuration["Vpic:BaseUrl"] ?? "https://vpic.nhtsa.dot.gov/api/vehicles/";
builder.Services
    .AddHttpClient<IVpicClient, VpicClient>(c =>
    {
        c.BaseAddress = new Uri(vpicBaseUrl);
        c.DefaultRequestHeaders.UserAgent.ParseAdd("VehicleExplorer/1.0");
    })
    // Retries with jittered backoff, per-attempt + total timeouts, and a circuit breaker.
    .AddStandardResilienceHandler();

builder.Services.AddScoped<IVehicleService, VehicleService>();

var app = builder.Build();

// Map an unreachable upstream to 502 instead of a generic 500, using RFC 7807 problem details.
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var isUpstream = error is VpicUnavailableException;
    context.Response.StatusCode = isUpstream ? StatusCodes.Status502BadGateway : StatusCodes.Status500InternalServerError;

    await context.RequestServices.GetRequiredService<IProblemDetailsService>().WriteAsync(new ProblemDetailsContext
    {
        HttpContext = context,
        ProblemDetails =
        {
            Status = context.Response.StatusCode,
            Title = isUpstream ? "Vehicle data source unavailable" : "Unexpected error",
            Detail = isUpstream ? "The NHTSA vPIC service could not be reached. Please try again shortly." : null,
        },
    });
}));

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi(); // /openapi/v1.json
}

// The Angular build is copied into wwwroot by the Dockerfile, so one container serves UI + API.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapHealthChecks("/health");

// Unknown /api/* paths are a real 404 (not the SPA's HTML); everything else falls back to the SPA.
app.Map("/api/{**rest}", () => Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Not found"));
app.MapFallbackToFile("index.html");

app.Run();

public partial class Program; // exposed for WebApplicationFactory in integration tests
