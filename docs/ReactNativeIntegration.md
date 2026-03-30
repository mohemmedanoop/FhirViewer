# React Native Integration Notes

This repository currently implements the Humana integration in ASP.NET Core so the OAuth exchange and client secret remain on the server.

## Recommended mobile pattern

1. React Native opens the backend endpoint `/auth/login` in a browser or secure web view.
2. Humana authenticates the member and collects consent.
3. The backend exchanges the authorization code for tokens at `/auth/token`.
4. The backend calls Humana FHIR APIs and returns sanitized view models or raw FHIR bundles to the mobile app.

## Why keep OAuth on the backend

- The Humana client secret should not be shipped inside a mobile binary.
- Redirect URI handling is simpler and more secure on the server.
- Token refresh can be centralized and audited.

## Suggested API shape for a future mobile client

- `GET /api/session`
- `GET /api/dashboard`
- `GET /api/resources/{resourceType}`
- `POST /auth/refresh`
- `POST /auth/logout`

## Suggested React Native screens

- Consent entry screen
- Member overview dashboard
- Claims and benefits screen
- Clinical timeline screen for observations, conditions, procedures, and immunizations
- Documents screen for `DocumentReference`
