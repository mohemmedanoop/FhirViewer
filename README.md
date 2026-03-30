# FHIR Viewer

This solution contains an ASP.NET Core Razor Pages app for patient-consented FHIR access. It is intentionally branded as a neutral FHIR Viewer so it can be adapted across payers, providers, or SMART on FHIR integrations.

## What it does

- Uses the authorization code flow for patient consent.
- Stores token details in ASP.NET Core session state.
- Supports an optional API key header for environments that require it.
- Retrieves these FHIR resources:
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
- Promotes `Patient` and `Coverage` into a featured summary header.
- Displays the remaining resources in easy horizontal card lanes.

## Configuration

Update [`src/FhirViewer.Web/appsettings.json`](C:\Project\FhirViewer\src\FhirViewer.Web\appsettings.json) or environment-specific overrides with the `FhirConnection` section:

- `FhirConnection:AuthorityBaseUrl`
- `FhirConnection:FhirBaseUrl`
- `FhirConnection:ClientId`
- `FhirConnection:ClientSecret`
- `FhirConnection:UseApiKey`
- `FhirConnection:ApiKeyHeaderName`
- `FhirConnection:ApiKey`
- `FhirConnection:RedirectPath`
- `FhirConnection:Scopes`
- `FhirConnection:RequestedResources`
- `FhirConnection:AppTitle`
- `FhirConnection:AppSubtitle`
- `FhirConnection:WelcomeTitle`
- `FhirConnection:WelcomeDescription`
- `FhirConnection:ConnectButtonLabel`
- `FhirConnection:LoginHint`

If `FhirConnection:UseApiKey` is `true`, the configured API key header is sent with token and FHIR API requests. If it is `false`, the viewer calls the API without that header.

## Run

```powershell
dotnet run --project .\src\FhirViewer.Web\FhirViewer.Web.csproj
```

The development startup flow can also open the browser automatically if `BrowserLaunch:Enabled` is `true`.

## React Native note

See [`docs/ReactNativeIntegration.md`](C:\Project\FhirViewer\docs\ReactNativeIntegration.md) for how to reuse the backend/auth pattern from a React Native or React Native Web client.
