namespace FhirViewer.Web.Models;

public sealed class DashboardViewModel
{
    public bool IsConfigured { get; init; }
    public bool IsAuthenticated { get; init; }
    public string? ErrorCode { get; init; }
    public OAuthTokenResponse? Token { get; init; }
    public IReadOnlyList<FhirSectionViewModel> Sections { get; init; } = [];
    public IReadOnlyList<string> RequestedResources { get; init; } = [];
    public FeaturedSummaryViewModel? PatientSummary { get; init; }
    public FeaturedSummaryViewModel? CoverageSummary { get; init; }
    public string LoginHint { get; init; } = "";
    public string ViewerTitle { get; init; } = "FHIR Viewer";
    public string ViewerSubtitle { get; init; } = "Configurable patient access experience";
    public string WelcomeTitle { get; init; } = "A simple, welcoming way to browse consented FHIR data.";
    public string WelcomeDescription { get; init; } = "";
    public string ConnectButtonLabel { get; init; } = "Connect";
}

public sealed class FeaturedSummaryViewModel
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public IReadOnlyList<FhirFactViewModel> Facts { get; init; } = [];
    public string ResourceType { get; init; } = "";
}

public sealed class FhirSectionViewModel
{
    public required string ResourceType { get; init; }
    public string Heading { get; init; } = "";
    public string Description { get; init; } = "";
    public string CategoryLabel { get; init; } = "FHIR Resource";
    public int Total { get; init; }
    public string? BundleJson { get; init; }
    public string? Error { get; init; }
    public IReadOnlyList<FhirCardViewModel> Cards { get; init; } = [];
}

public sealed class FhirCardViewModel
{
    public required string Title { get; init; }
    public string? Subtitle { get; init; }
    public IReadOnlyList<FhirFactViewModel> Facts { get; init; } = [];
    public string RawJson { get; init; } = "";
}

public sealed class FhirFactViewModel
{
    public required string Label { get; init; }
    public required string Value { get; init; }
}
