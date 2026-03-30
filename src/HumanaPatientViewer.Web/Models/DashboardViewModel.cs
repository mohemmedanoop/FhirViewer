namespace HumanaPatientViewer.Web.Models;

public sealed class DashboardViewModel
{
    public bool IsConfigured { get; init; }
    public bool IsAuthenticated { get; init; }
    public string? ErrorCode { get; init; }
    public HumanaTokenResponse? Token { get; init; }
    public IReadOnlyList<FhirSectionViewModel> Sections { get; init; } = [];
    public IReadOnlyList<string> RequestedResources { get; init; } = [];
    public string LoginHint { get; init; } = "Sandbox users follow HUser00001 / PW00001! through HUser00020 / PW00020!.";
}

public sealed class FhirSectionViewModel
{
    public required string ResourceType { get; init; }
    public string Heading { get; init; } = "";
    public string Description { get; init; } = "";
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
