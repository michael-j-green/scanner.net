# AGENTS.md

## Purpose

Operational guide for humans and coding agents working in this repository.

## Repository Layout

- scanner.net.slnx
- src/scanner.net
  - scanner.net.csproj
  - Program.cs
  - Models/
  - Services/
  - Configuration/
- Dockerfile
- docker-compose.yml
- .github/workflows/validate-pr.yml
- .github/workflows/release-ghcr.yml

## Build and Test Checklist

Run before pushing:

1. dotnet build src/scanner.net/scanner.net.csproj
2. docker build --platform linux/amd64 -t scanner-net-test .
3. docker compose config

## Release Policy

- Pull requests are validated by `.github/workflows/validate-pr.yml`.
- Configure the `Validate PR / validate` status check as required in GitHub branch protection to block merges on failed builds.
- Use GitHub Releases to publish images.
- Release name/tag must be:
  - v1.0.0
  - v1.0.0.a
- Workflow publishes GHCR image tags for raw and normalized versions.
- Workflow publishes multi-arch images for linux/amd64 and linux/arm64.

## Update Policy (Keep In Sync)

If you change behavior, structure, dependencies, or release flow, update in the same change:

- README.md
- docker-compose.yml
- Dockerfile
- .github/workflows/release-ghcr.yml
- .github/copilot-instructions.md
- .github/instructions/repository.instructions.md
- AGENTS.md
