using System.ComponentModel.DataAnnotations;

namespace HumanaPatientViewer.Web.Options;

public sealed class HumanaOptions
{
    public const string SectionName = "Humana";

    [Required]
    public string AuthorityBaseUrl { get; set; } = "https://sandbox-fhir.humana.com";

    [Required]
    public string FhirBaseUrl { get; set; } = "https://sandbox-fhir.humana.com/api";

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

    public bool HasRequiredConfiguration() =>
        !string.IsNullOrWhiteSpace(ClientId) &&
        !string.IsNullOrWhiteSpace(ClientSecret) &&
        !string.IsNullOrWhiteSpace(AuthorityBaseUrl) &&
        !string.IsNullOrWhiteSpace(FhirBaseUrl) &&
        !string.IsNullOrWhiteSpace(RedirectPath) &&
        (!UseApiKey || !string.IsNullOrWhiteSpace(ApiKey));
}
