using FhirViewer.Web.Models;
using FhirViewer.Web.Options;
using FhirViewer.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace FhirViewer.Web.Pages;

public class IndexModel(
    ViewerSessionStore sessionStore,
    DashboardService dashboardService,
    IOptions<FhirConnectionOptions> options) : PageModel
{
    private readonly FhirConnectionOptions _options = options.Value;

    public DashboardViewModel Dashboard { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var token = sessionStore.GetToken();
        var errorCode = Request.Query["error"].ToString();
        var isConfigured = _options.HasRequiredConfiguration();

        IReadOnlyList<FhirSectionViewModel> sections = [];
        if (token is not null)
        {
            sections = await dashboardService.BuildSectionsAsync(token.AccessToken, cancellationToken);
        }

        Dashboard = new DashboardViewModel
        {
            IsConfigured = isConfigured,
            IsAuthenticated = token is not null,
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? null : errorCode,
            Token = token,
            Sections = sections,
            RequestedResources = _options.RequestedResources,
            PatientSummary = DashboardService.BuildFeaturedSummary(sections, "Patient"),
            CoverageSummary = DashboardService.BuildFeaturedSummary(sections, "Coverage"),
            LoginHint = _options.LoginHint,
            ViewerTitle = _options.AppTitle,
            ViewerSubtitle = _options.AppSubtitle,
            WelcomeTitle = _options.WelcomeTitle,
            WelcomeDescription = _options.WelcomeDescription,
            ConnectButtonLabel = _options.ConnectButtonLabel
        };
    }
}
