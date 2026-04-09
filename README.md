# scanner.net

A small .NET 10 service for Brother network scan buttons.

It advertises scanner menu entries over SNMP, listens for button presses over UDP, pulls pages with `brother-scan-cli`, converts images to PDF, and writes one final PDF per scan.

## Repository Layout

- `scanner.net.slnx` - solution file
- `src/scanner.net` - application source (`.csproj`, services, models, config)
- `Dockerfile` - multi-stage image build
- `.github/workflows` - PR validation and release automation

## How It Works

1. `SnmpAdvertiseService` sends periodic Brother scan-menu advertisements to the printer.
2. `UdpListenerService` listens on `54925/udp`, parses `FUNC` + `USER`, echoes ACK, and queues the request.
3. `ScanWorkerService` runs `brother-scan-cli`, receives page images, converts each page to PDF, merges in temp, then moves the final PDF into output.

ADF and flatbed both produce a single output PDF named:

- `yyyyMMddHHmmss.pdf`

## Runtime Dependencies (inside container)

- `brother-scan-cli` (compiled from `rumpeltux/brother-scand`)
- `PdfSharpCore` (NuGet) for image -> PDF conversion and PDF merge
- `pdftk` for duplex odd/even shuffle mode

## Configuration

Configuration sources (in order):

- `appsettings.json`
- optional file set by `CONFIG_FILE`
- environment variables

### Core Environment Variables

- `SCANNER_IP` (required)
- `BIND_IP` (default `0.0.0.0`)
- `BIND_PORT` (default `54925`)
- `ADVERTISE_IP` (default `BIND_IP`)
- `ADVERTISE_PORT` (default `BIND_PORT`)
- `SNMP_COMMUNITY` (default `internal`)
- `SNMP_PORT` (default `161`)
- `CONFIG_INTERVAL_SECONDS` (default `600`)
- `OUTPUT_DIR` (default `/output/scan`)
- `TEMP_DIR` (default `/tmp/scan`)

### Profile Overrides via ENV

- `FLATBED_RESOLUTION`
- `ADF_RESOLUTION`
- `FLATBED_OUTPUT_DIR`
- `ADF_OUTPUT_DIR`

Additional options can be supplied via:

- `MENU_JSON` (full menu replacement)
- `SANE_DEVICE` (default profile device, if needed)

## Install / Run

### Docker build

```bash
docker build -t scanner-net .
```

### Docker run

```bash
docker run -d --name scanner-net-live --rm \
  --platform linux/amd64 \
  --privileged \
  -e SCANNER_IP=10.0.0.9 \
  -e ADVERTISE_IP=10.0.0.200 \
  -e BIND_IP=0.0.0.0 \
  -e CONFIG_INTERVAL_SECONDS=600 \
  -e OUTPUT_DIR=/output/scan \
  -p 54925:54925/udp \
  -v "$PWD/test-output:/output/scan" \
  scanner-net
```

### Docker Compose

Use the included `docker-compose.yml` as a production-style baseline.

## Releases and Versioning

- Pull requests run `.github/workflows/validate-pr.yml`, which builds the solution, checks `docker-compose.yml`, and builds the container for `linux/amd64` and `linux/arm64`.
- Mark the `Validate PR / validate` check as required in GitHub branch protection if you want builds enforced before merge.
- GitHub Releases (including pre-releases) publish a container image to GHCR.
- Release names must be version tags like `v1.0.0` or `v1.0.0.a`.
- The workflow embeds version metadata into the published executable via `Version` and `InformationalVersion`.
- Published container platforms: `linux/amd64`, `linux/arm64`.
- `linux/arm/v7` and `linux/386` are not published because the current .NET 10 preview SDK/runtime stack does not build cleanly for those targets in this image.
