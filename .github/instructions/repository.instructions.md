---
applyTo: "**"
---

Repository conventions for scanner.net:

- Keep solution layout as:
  - scanner.net.slnx
  - src/scanner.net/**
- Keep project file at src/scanner.net/scanner.net.csproj.
- Keep PR validation pipeline at .github/workflows/validate-pr.yml.
- Keep release pipeline at .github/workflows/release-ghcr.yml.
- Keep docs concise and current.

Before completing tasks that change behavior or structure, verify and update:

- README.md
- docker-compose.yml
- Dockerfile
- .github/copilot-instructions.md
- AGENTS.md
