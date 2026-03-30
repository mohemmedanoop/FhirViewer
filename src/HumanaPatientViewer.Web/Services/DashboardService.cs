using System.Text.Json;
using HumanaPatientViewer.Web.Models;
using HumanaPatientViewer.Web.Options;
using Microsoft.Extensions.Options;

namespace HumanaPatientViewer.Web.Services;

public sealed class DashboardService(HumanaFhirService fhirService, IOptions<HumanaOptions> options)
{
    private readonly HumanaOptions _options = options.Value;

    public async Task<IReadOnlyList<FhirSectionViewModel>> BuildSectionsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var tasks = _options.RequestedResources.Select(resourceType => BuildSectionAsync(resourceType, accessToken, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private async Task<FhirSectionViewModel> BuildSectionAsync(
        string resourceType,
        string accessToken,
        CancellationToken cancellationToken)
    {
        try
        {
            var bundleJson = await fhirService.GetBundleAsync(resourceType, accessToken, cancellationToken);
            return FhirDisplayMapper.MapSection(resourceType, bundleJson);
        }
        catch (Exception ex)
        {
            return new FhirSectionViewModel
            {
                ResourceType = resourceType,
                Heading = resourceType,
                Description = FhirDisplayMapper.GetDescription(resourceType),
                Error = ex.Message
            };
        }
    }
}

internal static class FhirDisplayMapper
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public static FhirSectionViewModel MapSection(string resourceType, string bundleJson)
    {
        using var document = JsonDocument.Parse(bundleJson);
        var root = document.RootElement;
        var cards = new List<FhirCardViewModel>();

        if (root.TryGetProperty("entry", out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray().Take(8))
            {
                if (entry.TryGetProperty("resource", out var resource))
                {
                    cards.Add(MapCard(resourceType, resource));
                }
            }
        }

        var total = root.TryGetProperty("total", out var totalElement) && totalElement.TryGetInt32(out var parsedTotal)
            ? parsedTotal
            : cards.Count;

        return new FhirSectionViewModel
        {
            ResourceType = resourceType,
            Heading = resourceType switch
            {
                "ExplanationOfBenefit" => "Explanation of Benefits",
                "MedicationRequest" => "Medication Requests",
                "DocumentReference" => "Document References",
                _ => resourceType
            },
            Description = GetDescription(resourceType),
            Total = total,
            BundleJson = bundleJson,
            Cards = cards
        };
    }

    public static string GetDescription(string resourceType) => resourceType switch
    {
        "Patient" => "Demographics and core member identity details.",
        "Coverage" => "Plan and payer coverage currently associated with the member.",
        "ExplanationOfBenefit" => "Claims and adjudication details for services already processed.",
        "Goal" => "Documented care goals and expected outcomes.",
        "Immunization" => "Vaccination history and status.",
        "MedicationRequest" => "Medication orders and prescriptions.",
        "Observation" => "Clinical observations such as labs and vitals.",
        "Procedure" => "Completed procedures and related servicing details.",
        "CarePlan" => "Coordinated plans of care.",
        "CareTeam" => "Care participants supporting the member.",
        "Condition" => "Diagnoses and problem list items.",
        "DocumentReference" => "Files and clinical documents shared through FHIR.",
        _ => "FHIR data returned by Humana."
    };

    private static FhirCardViewModel MapCard(string resourceType, JsonElement resource)
    {
        var title = resourceType switch
        {
            "Patient" => ReadPatientName(resource),
            "Coverage" => ReadText(resource, "type", "text") ?? ReadFirstCoding(resource, "type") ?? "Coverage",
            "ExplanationOfBenefit" => ReadText(resource, "type", "text") ?? "Claim summary",
            "Goal" => ReadText(resource, "description", "text") ?? "Care goal",
            "Immunization" => ReadText(resource, "vaccineCode", "text") ?? ReadFirstCoding(resource, "vaccineCode") ?? "Immunization",
            "MedicationRequest" => ReadText(resource, "medicationCodeableConcept", "text") ?? ReadFirstCoding(resource, "medicationCodeableConcept") ?? "Medication request",
            "Observation" => ReadText(resource, "code", "text") ?? ReadFirstCoding(resource, "code") ?? "Observation",
            "Procedure" => ReadText(resource, "code", "text") ?? ReadFirstCoding(resource, "code") ?? "Procedure",
            "CarePlan" => ReadString(resource, "title") ?? ReadText(resource, "category", "text") ?? "Care plan",
            "CareTeam" => ReadString(resource, "name") ?? "Care team",
            "Condition" => ReadText(resource, "code", "text") ?? ReadFirstCoding(resource, "code") ?? "Condition",
            "DocumentReference" => ReadText(resource, "type", "text") ?? ReadFirstCoding(resource, "type") ?? "Document reference",
            _ => ReadString(resource, "id") ?? resourceType
        };

        var subtitle = resourceType switch
        {
            "Patient" => Combine("Patient", ReadString(resource, "gender"), ReadString(resource, "birthDate")),
            "Coverage" => Combine("Coverage", ReadString(resource, "status"), ReadString(resource, "subscriberId")),
            "ExplanationOfBenefit" => Combine("EOB", ReadString(resource, "status"), ReadString(resource, "use")),
            "Goal" => Combine("Goal", ReadString(resource, "lifecycleStatus"), ReadDate(resource, "startDate")),
            "Immunization" => Combine("Immunization", ReadString(resource, "status"), ReadDate(resource, "occurrenceDateTime")),
            "MedicationRequest" => Combine("Medication", ReadString(resource, "status"), ReadDate(resource, "authoredOn")),
            "Observation" => Combine("Observation", ReadObservationValue(resource), ReadDate(resource, "effectiveDateTime")),
            "Procedure" => Combine("Procedure", ReadString(resource, "status"), ReadDate(resource, "performedDateTime") ?? ReadPeriod(resource, "performedPeriod")),
            "CarePlan" => Combine("Care plan", ReadString(resource, "status"), ReadPeriod(resource, "period")),
            "CareTeam" => Combine("Care team", ReadString(resource, "status"), ReadDate(resource, "period", "start")),
            "Condition" => Combine("Condition", ReadClinicalStatus(resource), ReadDate(resource, "onsetDateTime")),
            "DocumentReference" => Combine("Document", ReadString(resource, "status"), ReadDate(resource, "date")),
            _ => null
        };

        return new FhirCardViewModel
        {
            Title = title,
            Subtitle = subtitle,
            Facts = BuildFacts(resourceType, resource),
            RawJson = JsonSerializer.Serialize(resource, IndentedJson)
        };
    }

    private static IReadOnlyList<FhirFactViewModel> BuildFacts(string resourceType, JsonElement resource)
    {
        var facts = resourceType switch
        {
            "Patient" => new[]
            {
                Fact("Member ID", ReadString(resource, "id")),
                Fact("Telecom", ReadArrayText(resource, "telecom", "value")),
                Fact("Address", ReadAddress(resource))
            },
            "Coverage" => new[]
            {
                Fact("Payor", ReadArrayText(resource, "payor", "display")),
                Fact("Relationship", ReadText(resource, "relationship", "text") ?? ReadFirstCoding(resource, "relationship")),
                Fact("Period", ReadPeriod(resource, "period"))
            },
            "ExplanationOfBenefit" => new[]
            {
                Fact("Outcome", ReadString(resource, "outcome")),
                Fact("Billable", ReadPeriod(resource, "billablePeriod")),
                Fact("Insurer", ReadNestedDisplay(resource, "insurer"))
            },
            "Goal" => new[]
            {
                Fact("Status", ReadString(resource, "lifecycleStatus")),
                Fact("Priority", ReadText(resource, "priority", "text") ?? ReadFirstCoding(resource, "priority")),
                Fact("Target", ReadGoalTarget(resource))
            },
            "Immunization" => new[]
            {
                Fact("Occurrence", ReadDate(resource, "occurrenceDateTime")),
                Fact("Lot", ReadString(resource, "lotNumber")),
                Fact("Performer", ReadArrayText(resource, "performer", "actor", "display"))
            },
            "MedicationRequest" => new[]
            {
                Fact("Intent", ReadString(resource, "intent")),
                Fact("Requester", ReadNestedDisplay(resource, "requester")),
                Fact("Dosage", ReadArrayText(resource, "dosageInstruction", "text"))
            },
            "Observation" => new[]
            {
                Fact("Value", ReadObservationValue(resource)),
                Fact("Category", ReadArrayText(resource, "category", "text")),
                Fact("Issued", ReadDate(resource, "issued"))
            },
            "Procedure" => new[]
            {
                Fact("Performed", ReadDate(resource, "performedDateTime") ?? ReadPeriod(resource, "performedPeriod")),
                Fact("Category", ReadText(resource, "category", "text") ?? ReadFirstCoding(resource, "category")),
                Fact("Performer", ReadArrayText(resource, "performer", "actor", "display"))
            },
            "CarePlan" => new[]
            {
                Fact("Intent", ReadString(resource, "intent")),
                Fact("Category", ReadArrayText(resource, "category", "text")),
                Fact("Period", ReadPeriod(resource, "period"))
            },
            "CareTeam" => new[]
            {
                Fact("Category", ReadArrayText(resource, "category", "text")),
                Fact("Subject", ReadNestedDisplay(resource, "subject")),
                Fact("Participants", CountArray(resource, "participant")?.ToString())
            },
            "Condition" => new[]
            {
                Fact("Clinical Status", ReadClinicalStatus(resource)),
                Fact("Verification", ReadVerificationStatus(resource)),
                Fact("Recorded", ReadDate(resource, "recordedDate"))
            },
            "DocumentReference" => new[]
            {
                Fact("Category", ReadArrayText(resource, "category", "text")),
                Fact("Author", ReadArrayText(resource, "author", "display")),
                Fact("Created", ReadDate(resource, "date"))
            },
            _ => new[] { Fact("Resource ID", ReadString(resource, "id")) }
        };

        return facts.Where(static x => x is not null).Cast<FhirFactViewModel>().ToArray();
    }

    private static FhirFactViewModel? Fact(string label, string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : new FhirFactViewModel { Label = label, Value = value };

    private static string ReadPatientName(JsonElement resource)
    {
        if (resource.TryGetProperty("name", out var names) && names.ValueKind == JsonValueKind.Array)
        {
            var first = names.EnumerateArray().FirstOrDefault();
            var given = first.TryGetProperty("given", out var givenNames) && givenNames.ValueKind == JsonValueKind.Array
                ? string.Join(' ', givenNames.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
                : null;
            var family = first.TryGetProperty("family", out var familyName) ? familyName.GetString() : null;
            var name = string.Join(' ', new[] { given, family }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(name))
            {
                return name;
            }
        }

        return "Patient";
    }

    private static string? ReadObservationValue(JsonElement resource)
    {
        if (resource.TryGetProperty("valueQuantity", out var quantity))
        {
            return Combine(null, ReadString(quantity, "value"), ReadString(quantity, "unit"));
        }

        if (resource.TryGetProperty("valueString", out var valueString))
        {
            return valueString.GetString();
        }

        return ReadText(resource, "valueCodeableConcept", "text") ?? ReadFirstCoding(resource, "valueCodeableConcept");
    }

    private static string? ReadGoalTarget(JsonElement resource)
    {
        if (!resource.TryGetProperty("target", out var target) || target.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = target.EnumerateArray().FirstOrDefault();
        return ReadText(first, "measure", "text") ?? ReadFirstCoding(first, "measure") ?? ReadDate(first, "dueDate");
    }

    private static string? ReadClinicalStatus(JsonElement resource) =>
        ReadText(resource, "clinicalStatus", "text") ?? ReadFirstCoding(resource, "clinicalStatus");

    private static string? ReadVerificationStatus(JsonElement resource) =>
        ReadText(resource, "verificationStatus", "text") ?? ReadFirstCoding(resource, "verificationStatus");

    private static string? ReadAddress(JsonElement resource)
    {
        if (!resource.TryGetProperty("address", out var addresses) || addresses.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var first = addresses.EnumerateArray().FirstOrDefault();
        var line = first.TryGetProperty("line", out var lines) && lines.ValueKind == JsonValueKind.Array
            ? string.Join(", ", lines.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrWhiteSpace(x)))
            : null;
        return Combine(null, line, ReadString(first, "city"), ReadString(first, "state"));
    }

    private static string? ReadNestedDisplay(JsonElement resource, params string[] path)
    {
        var element = Navigate(resource, path);
        return element is null ? null : ReadString(element.Value, "display") ?? ReadString(element.Value, "reference");
    }

    private static int? CountArray(JsonElement resource, string propertyName) =>
        resource.TryGetProperty(propertyName, out var element) && element.ValueKind == JsonValueKind.Array
            ? element.GetArrayLength()
            : null;

    private static string? ReadArrayText(JsonElement resource, params string[] path)
    {
        if (path.Length == 0 || !resource.TryGetProperty(path[0], out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var remainingPath = path.Skip(1).ToArray();
        var values = new List<string>();

        foreach (var item in array.EnumerateArray())
        {
            var current = item;
            foreach (var segment in remainingPath)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    current = default;
                    break;
                }
            }

            if (current.ValueKind == JsonValueKind.String)
            {
                var value = current.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    values.Add(value);
                }
            }
        }

        return values.Count == 0 ? null : string.Join(", ", values.Distinct());
    }

    private static string? ReadDate(JsonElement resource, params string[] path)
    {
        var element = Navigate(resource, path);
        return element is null || element.Value.ValueKind != JsonValueKind.String ? null : element.Value.GetString();
    }

    private static string? ReadPeriod(JsonElement resource, string propertyName)
    {
        if (!resource.TryGetProperty(propertyName, out var period))
        {
            return null;
        }

        return Combine(null, ReadString(period, "start"), ReadString(period, "end"));
    }

    private static string? ReadText(JsonElement resource, params string[] path)
    {
        var element = Navigate(resource, path);
        return element is null || element.Value.ValueKind != JsonValueKind.String ? null : element.Value.GetString();
    }

    private static string? ReadFirstCoding(JsonElement resource, string propertyName)
    {
        if (!resource.TryGetProperty(propertyName, out var node))
        {
            return null;
        }

        if (node.TryGetProperty("coding", out var coding) && coding.ValueKind == JsonValueKind.Array)
        {
            var first = coding.EnumerateArray().FirstOrDefault();
            return ReadString(first, "display") ?? ReadString(first, "code");
        }

        return null;
    }

    private static string? ReadString(JsonElement resource, string propertyName)
    {
        if (!resource.TryGetProperty(propertyName, out var node))
        {
            return null;
        }

        return node.ValueKind switch
        {
            JsonValueKind.String => node.GetString(),
            JsonValueKind.Number => node.ToString(),
            _ => null
        };
    }

    private static JsonElement? Navigate(JsonElement resource, params string[] path)
    {
        var current = resource;
        foreach (var segment in path)
        {
            if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
            {
                return null;
            }
        }

        return current;
    }

    private static string? Combine(string? prefix, params string?[] values)
    {
        var parts = values.Where(x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
        if (parts.Count == 0)
        {
            return prefix;
        }

        return string.IsNullOrWhiteSpace(prefix)
            ? string.Join(" • ", parts)
            : $"{prefix} • {string.Join(" • ", parts)}";
    }
}
