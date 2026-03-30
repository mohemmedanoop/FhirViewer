using HumanaPatientViewer.Web.Options;
using HumanaPatientViewer.Web.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".HumanaPatientViewer.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(4);
});

builder.Services
    .AddOptions<HumanaOptions>()
    .Bind(builder.Configuration.GetSection(HumanaOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<BrowserLaunchOptions>()
    .Bind(builder.Configuration.GetSection(BrowserLaunchOptions.SectionName));

builder.Services.AddHttpClient<HumanaAuthService>();
builder.Services.AddHttpClient<HumanaFhirService>();
builder.Services.AddScoped<HumanaSessionStore>();
builder.Services.AddScoped<DashboardService>();

if (builder.Environment.IsDevelopment())
{
    builder.Services.AddHostedService<BrowserLauncherHostedService>();
}

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();
app.UseSession();
app.UseAuthorization();

app.MapGet("/auth/login", (
    HttpContext httpContext,
    HumanaSessionStore sessionStore,
    HumanaAuthService authService,
    IOptions<HumanaOptions> options) =>
{
    if (!options.Value.HasRequiredConfiguration())
    {
        return Results.Redirect("/?error=config");
    }

    var state = Guid.NewGuid().ToString("N");
    sessionStore.SetOAuthState(state);

    var authorizationUrl = authService.BuildAuthorizationUrl(httpContext, state);
    return Results.Redirect(authorizationUrl);
});

app.MapGet("/auth/callback", async (
    HttpContext httpContext,
    HumanaSessionStore sessionStore,
    HumanaAuthService authService,
    string? code,
    string? state,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(state))
    {
        return Results.Redirect("/?error=callback");
    }

    var expectedState = sessionStore.GetOAuthState();
    if (!string.Equals(expectedState, state, StringComparison.Ordinal))
    {
        return Results.Redirect("/?error=state");
    }

    var token = await authService.ExchangeCodeAsync(httpContext, code, cancellationToken);
    sessionStore.SetToken(token);
    sessionStore.ClearOAuthState();

    return Results.Redirect("/");
});

app.MapPost("/auth/logout", (HumanaSessionStore sessionStore) =>
{
    sessionStore.ClearAll();
    return Results.Redirect("/");
});

app.MapPost("/auth/refresh", async (
    HumanaSessionStore sessionStore,
    HumanaAuthService authService,
    HttpContext httpContext,
    CancellationToken cancellationToken) =>
{
    var currentToken = sessionStore.GetToken();
    if (currentToken is null || string.IsNullOrWhiteSpace(currentToken.RefreshToken))
    {
        return Results.Redirect("/?error=refresh");
    }

    var refreshed = await authService.RefreshTokenAsync(httpContext, currentToken.RefreshToken, cancellationToken);
    sessionStore.SetToken(refreshed);
    return Results.Redirect("/");
});

app.MapGet("/api/session", (HumanaSessionStore sessionStore) =>
{
    var token = sessionStore.GetToken();
    return Results.Ok(new
    {
        isAuthenticated = token is not null,
        patient = token?.Patient,
        expiresAtUtc = token?.ExpiresAtUtc,
        scope = token?.Scope
    });
});

app.MapGet("/api/dashboard", async (
    HumanaSessionStore sessionStore,
    DashboardService dashboardService,
    CancellationToken cancellationToken) =>
{
    var token = sessionStore.GetToken();
    if (token is null)
    {
        return Results.Unauthorized();
    }

    var sections = await dashboardService.BuildSectionsAsync(token.AccessToken, cancellationToken);
    return Results.Ok(new
    {
        patient = token.Patient,
        expiresAtUtc = token.ExpiresAtUtc,
        scope = token.Scope,
        sections
    });
});

app.MapGet("/api/resources/{resourceType}", async (
    string resourceType,
    HumanaSessionStore sessionStore,
    HumanaFhirService fhirService,
    CancellationToken cancellationToken) =>
{
    var token = sessionStore.GetToken();
    if (token is null)
    {
        return Results.Unauthorized();
    }

    var bundle = await fhirService.GetBundleAsync(resourceType, token.AccessToken, cancellationToken);
    return Results.Content(bundle, "application/json");
});

app.UseStaticFiles();
app.MapRazorPages();

app.Run();
