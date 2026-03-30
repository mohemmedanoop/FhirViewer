using FhirViewer.Web.Options;
using FhirViewer.Web.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddRazorPages();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.Cookie.Name = ".FhirViewer.Session";
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.IdleTimeout = TimeSpan.FromHours(4);
});

builder.Services
    .AddOptions<FhirConnectionOptions>()
    .Bind(builder.Configuration.GetSection(FhirConnectionOptions.SectionName))
    .ValidateDataAnnotations();

builder.Services
    .AddOptions<BrowserLaunchOptions>()
    .Bind(builder.Configuration.GetSection(BrowserLaunchOptions.SectionName));

builder.Services.AddHttpClient<OAuthService>();
builder.Services.AddHttpClient<FhirApiService>();
builder.Services.AddScoped<ViewerSessionStore>();
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
    ViewerSessionStore sessionStore,
    OAuthService authService,
    IOptions<FhirConnectionOptions> options) =>
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
    ViewerSessionStore sessionStore,
    OAuthService authService,
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

app.MapPost("/auth/logout", (ViewerSessionStore sessionStore) =>
{
    sessionStore.ClearAll();
    return Results.Redirect("/");
});

app.MapPost("/auth/refresh", async (
    ViewerSessionStore sessionStore,
    OAuthService authService,
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

app.MapGet("/api/session", (ViewerSessionStore sessionStore) =>
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
    ViewerSessionStore sessionStore,
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
    ViewerSessionStore sessionStore,
    FhirApiService fhirService,
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
