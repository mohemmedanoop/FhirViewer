using System.ComponentModel.DataAnnotations;

namespace FhirViewer.Web.Options;

public sealed class FhirConnectionOptions
{
    public const string SectionName = "FhirConnection";

    [Required]
    public string AuthorityBaseUrl { get; set; } = "https://your-oauth-server";

    [Required]
    public string FhirBaseUrl { get; set; } = "https://your-fhir-server";

    [Required]
    public string ClientId { get; set; } = "";

    [Required]
    public string ClientSecret { get; set; } = "";

    [Required]
    public string RedirectPath { get; set; } = "/auth/callback";

    public bool UseApiKey { get; set; }

    public string ApiKeyHeaderName { get; set; } = "x-api-key";

    public string? ApiKey { get; set; }

    public string[] Scopes { get; set; } =
    [
        "openid",
        "offline_access",
        "launch/patient",
        "patient/Patient.read",
        "patient/Coverage.read",
        "patient/ExplanationOfBenefit.read",
        "patient/Procedure.read",
        "patient/MedicationRequest.read",
        "patient/Immunization.read",
        "patient/CareTeam.read",
        "patient/Condition.read",
        "patient/CarePlan.read",
        "patient/Observation.read",
        "patient/Goal.read",
        "patient/DocumentReference.read"
    ];

    public string[] RequestedResources { get; set; } =
    [
        "Patient",
        "Coverage",
        "ExplanationOfBenefit",
        "Goal",
        "Immunization",
        "MedicationRequest",
        "Observation",
        "Procedure",
        "CarePlan",
        "CareTeam",
        "Condition",
        "DocumentReference"
    ];

    public string AppTitle { get; set; } = "FHIR Viewer";

    public string AppSubtitle { get; set; } = "Configurable patient access experience";

    public string WelcomeTitle { get; set; } = "A simple, welcoming way to browse consented FHIR data.";

    public string WelcomeDescription { get; set; } =
        "Connect once, capture patient consent, and explore claims, coverage, medications, documents, and clinical details in one approachable dashboard.";

    public string ConnectButtonLabel { get; set; } = "Connect and view records";

    public string LoginHint { get; set; } =
        "Use your configured SMART on FHIR or payer sandbox credentials here. Update this hint per environment if your consent flow changes.";

    public bool HasRequiredConfiguration() =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(AuthorityBaseUrl) &&
        !string.IsNullOrWhiteSpace(FhirBaseUrl) &&
        !string.IsNullOrWhiteSpace(RedirectPath) &&
        (!UseApiKey || !string.IsNullOrWhiteSpace(ApiKey));
}
