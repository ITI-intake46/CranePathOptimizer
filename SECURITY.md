# CraneOpt security and privacy controls — REV05

## AI destinations

Provider branding is not a trust boundary. CraneOpt classifies the actual configured endpoint URI. Only HTTP/HTTPS loopback destinations (`localhost`, `127.0.0.1`, `::1`) are treated as local. A remotely hosted Ollama endpoint is remote even if a stored profile previously carried `IsLocal=true`. Remote project-context transmission requires explicit provider consent, and insecure remote HTTP is rejected.

## Local data and persistence

The shipped canonical catalog is immutable application content. The writable catalog lives under the user's Local Application Data (or an explicitly configured `CRANEOPT_CATALOG_PATH`). Before any migration/mutation of an existing writable catalog, CraneOpt creates and SHA-256-verifies a non-empty backup; failure aborts migration.

## Release controls

REV05 Windows packaging requires: pinned SDK, locked restore, full tests, native xBIM geometry execution, dependency vulnerability audit, SPDX SBOM, SHA-256 manifest, exact source commit/tag and Git bundle, Authenticode signing and post-signature verification. The release pipeline fails closed on any missing evidence.

## Reporting a vulnerability

Do not place credentials or private project data in a public issue. Report security findings privately to the project owner/release authority responsible for this deployment.
