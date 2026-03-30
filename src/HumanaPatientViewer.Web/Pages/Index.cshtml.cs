using HumanaPatientViewer.Web.Models;
using HumanaPatientViewer.Web.Options;
using HumanaPatientViewer.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;

namespace HumanaPatientViewer.Web.Pages;

public class IndexModel(
    HumanaSessionStore sessionStore,
    DashboardService dashboardService,
    IOptions<HumanaOptions> options) : PageModel
{
    private readonly HumanaOptions _options = options.Value;

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
            RequestedResources = _options.RequestedResources
        };
    }
}
