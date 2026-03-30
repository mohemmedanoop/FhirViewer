using System.Text.Json;
using FhirViewer.Web.Models;
using FhirViewer.Web.Options;
using Microsoft.Extensions.Options;

namespace FhirViewer.Web.Services;

public sealed class DashboardService(FhirApiService fhirService, IOptions<FhirConnectionOptions> options)
{
    private readonly FhirConnectionOptions _options = options.Value;

    public async Task<IReadOnlyList<FhirSectionViewModel>> BuildSectionsAsync(
        string accessToken,
        CancellationToken cancellationToken)
    {
        var requestedResources = _options.RequestedResources
            .Where(static resourceType => !string.IsNullOrWhiteSpace(resourceType))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tasks = requestedResources.Select(resourceType => BuildSectionAsync(resourceType, accessToken, cancellationToken));
        var sections = await Task.WhenAll(tasks);

        return sections
            .GroupBy(static section => section.ResourceType, StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .OrderBy(static section => GetDisplayPriority(section.ResourceType))
            .ToArray();
    }

    public static FeaturedSummaryViewModel? BuildFeaturedSummary(IReadOnlyList<FhirSectionViewModel> sections, string resourceType)
    {
        var section = sections.FirstOrDefault(x => string.Equals(x.ResourceType, resourceType, StringComparison.OrdinalIgnoreCase));
        var card = section?.Cards.FirstOrDefault();
        if (card is null)
        {
            return null;
        }

        return new FeaturedSummaryViewModel
        {
            ResourceType = resourceType,
            Title = card.Title,
            Subtitle = card.Subtitle,
            Facts = card.Facts.Take(4).ToArray()
        };
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
                Heading = FhirDisplayMapper.GetHeading(resourceType),
                Description = FhirDisplayMapper.GetDescription(resourceType),
                CategoryLabel = FhirDisplayMapper.GetCategory(resourceType),
                Error = ex.Message
            };
        }
    }

    private static int GetDisplayPriority(string resourceType) => resourceType switch
    {
        "Patient" => 0,
        "Coverage" => 1,
        "ExplanationOfBenefit" => 2,
        "Observation" => 3,
        "MedicationRequest" => 4,
        "Condition" => 5,
        _ => 10
    };
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
            foreach (var entry in entries.EnumerateArray().Take(10))
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
            Heading = GetHeading(resourceType),
            Description = GetDescription(resourceType),
            CategoryLabel = GetCategory(resourceType),
            Total = total,
            BundleJson = bundleJson,
            Cards = cards
        };
    }

    public static string GetHeading(string resourceType) => resourceType switch
    {
        "ExplanationOfBenefit" => "Explanation of Benefits",
        "MedicationRequest" => "Medication Requests",
        "DocumentReference" => "Documents",
        _ => resourceType
    };

    public static string GetDescription(string resourceType) => resourceType switch
    {
        "Patient" => "Identity, demographics, contact details, and core patient facts.",
        "Coverage" => "Insurance and payer coverage information that frames the rest of the experience.",
        "ExplanationOfBenefit" => "Claims, adjudication, and financial detail for completed services.",
        "Goal" => "Goals and target outcomes that give the member's plan context.",
        "Immunization" => "Vaccines and immunization history in a quick-review format.",
        "MedicationRequest" => "Current and historic medication orders and prescriptions.",
        "Observation" => "Lab results, vitals, and other clinical measurements.",
        "Procedure" => "Procedures and service events completed for the patient.",
        "CarePlan" => "Plans of care and coordinated next steps.",
        "CareTeam" => "Care participants and care relationships around the patient.",
        "Condition" => "Diagnoses, active problems, and longitudinal condition tracking.",
        "DocumentReference" => "Documents and files that can be opened or audited later.",
        _ => "FHIR data returned by the connected payer or provider platform."
    };

    public static string GetCategory(string resourceType) => resourceType switch
    {
        "Patient" => "Featured Profile",
        "Coverage" => "Featured Coverage",
        "ExplanationOfBenefit" => "Financial",
        "Goal" => "Care Planning",
        "Immunization" => "Preventive Care",
        "MedicationRequest" => "Medications",
        "Observation" => "Clinical Data",
        "Procedure" => "Clinical Data",
        "CarePlan" => "Care Planning",
        "CareTeam" => "Care Planning",
        "Condition" => "Clinical Data",
        "DocumentReference" => "Documents",
        _ => "FHIR Resource"
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
            "ExplanationOfBenefit" => Combine("Benefit", ReadString(resource, "status"), ReadString(resource, "use")),
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
                Fact("Patient ID", ReadString(resource, "id")),
                Fact("Telecom", ReadArrayText(resource, "telecom", "value")),
                Fact("Address", ReadAddress(resource)),
                Fact("Language", ReadArrayText(resource, "communication", "language", "text") ?? ReadArrayCoding(resource, "communication", "language"))
            },
            "Coverage" => new[]
            {
                Fact("Payor", ReadArrayText(resource, "payor", "display")),
                Fact("Relationship", ReadText(resource, "relationship", "text") ?? ReadFirstCoding(resource, "relationship")),
                Fact("Period", ReadPeriod(resource, "period")),
                Fact("Subscriber", ReadString(resource, "subscriberId"))
            },
            "ExplanationOfBenefit" => new[]
            {
                Fact("Outcome", ReadString(resource, "outcome")),
                Fact("Billable", ReadPeriod(resource, "billablePeriod")),
                Fact("Insurer", ReadNestedDisplay(resource, "insurer")),
                Fact("Disposition", ReadString(resource, "disposition"))
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
                ? string.Join(" ", givenNames.EnumerateArray().Select(static x => x.GetString()).Where(static x => !string.IsNullOrWhiteSpace(x)))
                : null;
            var family = first.TryGetProperty("family", out var familyName) ? familyName.GetString() : null;
            var name = string.Join(" ", new[] { given, family }.Where(static x => !string.IsNullOrWhiteSpace(x)));
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
            ? string.Join(", ", lines.EnumerateArray().Select(static x => x.GetString()).Where(static x => !string.IsNullOrWhiteSpace(x)))
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

    private static string? ReadArrayCoding(JsonElement resource, params string[] path)
    {
        if (path.Length == 0 || !resource.TryGetProperty(path[0], out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var item in array.EnumerateArray())
        {
            var current = item;
            foreach (var segment in path.Skip(1))
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    current = default;
                    break;
                }
            }

            var codingText = ReadFirstCoding(current, "coding");
            if (!string.IsNullOrWhiteSpace(codingText))
            {
                return codingText;
            }
        }

        return null;
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
        JsonElement node;
        if (string.Equals(propertyName, "coding", StringComparison.Ordinal))
        {
            node = resource;
        }
        else if (!resource.TryGetProperty(propertyName, out node))
        {
            return null;
        }

        if (node.TryGetProperty("coding", out var coding) && coding.ValueKind == JsonValueKind.Array)
        {
            var first = coding.EnumerateArray().FirstOrDefault();
            return ReadString(first, "display") ?? ReadString(first, "code");
        }

        if (node.ValueKind == JsonValueKind.Array)
        {
            var first = node.EnumerateArray().FirstOrDefault();
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
        var parts = values.Where(static x => !string.IsNullOrWhiteSpace(x)).Cast<string>().ToList();
        if (parts.Count == 0)
        {
            return prefix;
        }

        return string.IsNullOrWhiteSpace(prefix)
            ? string.Join(" | ", parts)
            : $"{prefix} | {string.Join(" | ", parts)}";
    }
}
