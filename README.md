# FHIR Viewer

This solution contains an ASP.NET Core Razor Pages app that integrates with Humana OAuth and retrieves member-scoped FHIR data after patient consent.

## What it does

- Uses the Humana authorization-code flow.
- Stores token details in ASP.NET Core session state.
- Retrieves these FHIR resources from Humana:
  - `Patient`
  - `Coverage`
  - `ExplanationOfBenefit`
  - `Goal`
  - `Immunization`
  - `MedicationRequest`
  - `Observation`
  - `Procedure`
  - `CarePlan`
  - `CareTeam`
  - `Condition`
  - `DocumentReference`
- Renders a browser dashboard with summaries and raw JSON.

## Configuration

Update [`src/HumanaPatientViewer.Web/appsettings.json`](C:\Project\FhirViewer\src\HumanaPatientViewer.Web\appsettings.json) or environment-specific overrides with:

- `Humana:AuthorityBaseUrl`
- `Humana:FhirBaseUrl`
- `Humana:ClientId`
- `Humana:ClientSecret`
- `Humana:UseApiKey`
- `Humana:ApiKeyHeaderName`
- `Humana:ApiKey`
- `Humana:RedirectPath`
- `Humana:Scopes`
- `Humana:RequestedResources`

If `Humana:UseApiKey` is `true`, the app adds the configured API key header to both token requests and FHIR API requests. If it is `false`, the app calls Humana without that header.

The default sandbox values are aligned with the Humana OAuth docs:

- Authorize endpoint: `https://sandbox-fhir.humana.com/auth/authorize`
- Token endpoint: `https://sandbox-fhir.humana.com/auth/token`
- FHIR base URL: `https://sandbox-fhir.humana.com/api`

## Run

```powershell
dotnet run --project .\src\HumanaPatientViewer.Web\HumanaPatientViewer.Web.csproj
```

Then open the local site, choose **Connect with Humana**, and complete the member consent flow.

## Sandbox test users

Humana documents sandbox credentials in this pattern:

- Username: `HUser00001` through `HUser00020`
- Password: `PW00001!` through `PW00020!`

## React Native note

See [`docs/ReactNativeIntegration.md`](C:\Project\FhirViewer\docs\ReactNativeIntegration.md) for how to reuse the same backend/auth approach from a React Native or React Native Web client.
