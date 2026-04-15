# Copilot Instructions

## Project Scope

This repository contains only the scanner.net .NET solution.

- Solution: scanner.net.slnx
- App project: src/scanner.net/scanner.net.csproj
- Container build: Dockerfile
- PR validation: .github/workflows/validate-pr.yml
- Release automation: .github/workflows/release-ghcr.yml

## Code Organization Rules

- Keep runtime code under src/scanner.net.
- Keep domain models under src/scanner.net/Models.
- Keep hosted services under src/scanner.net/Services.
- Keep options/configuration helpers under src/scanner.net/Configuration.
- Prefer one public class per file.

## Dependency Rules

- Prefer managed NuGet dependencies over shell tooling where practical.
- Keep native runtime dependencies in Dockerfile minimal.
- If a package is added/updated, ensure build and Docker build still pass.

## Documentation Update Policy

When behavior, structure, dependencies, or release flow changes, update all affected files in the same PR:

- README.md
- docker-compose.yml
- Dockerfile
- .github/workflows/release-ghcr.yml
- AGENTS.md
- .github/copilot-instructions.md
- .github/instructions/repository.instructions.md

## Release Version Rules

Release name/tag format:

- Stable: v1.0.0
- Pre-release: v1.0.0.a

CI converts pre-release format to semver-compatible build metadata for dotnet and container tags.
Pull requests are validated by .github/workflows/validate-pr.yml before merge when that check is required in branch protection.
PR validation includes an amd64 container startup smoke test.
Release workflow publishes multi-arch images: linux/amd64, linux/arm64.
