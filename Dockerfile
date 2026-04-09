# Stage 0: compile brother-scan-cli (pure C11, no external deps, cross-platform)
FROM debian:bookworm-slim AS brother-build
RUN apt-get update -q \
    && apt-get install -q -y --no-install-recommends git build-essential ca-certificates \
    && rm -rf /var/lib/apt/lists/*
RUN git clone --recurse-submodules https://github.com/rumpeltux/brother-scand.git /src \
    && cd /src && make -j$(nproc) build/brother-scan-cli

# Stage 1: build .NET app
FROM mcr.microsoft.com/dotnet/sdk:10.0-preview AS build
WORKDIR /src
ARG TARGETARCH
ARG APP_VERSION=0.0.0-dev
ARG APP_VERSION_RAW=v0.0.0-dev

COPY src/scanner.net/scanner.net.csproj src/scanner.net/
RUN dotnet restore src/scanner.net/scanner.net.csproj

COPY src/scanner.net/ src/scanner.net/
RUN case "$TARGETARCH" in \
    "arm64") RID="linux-arm64" ;; \
    "amd64") RID="linux-x64" ;; \
    *) echo "Unsupported TARGETARCH: $TARGETARCH"; exit 1 ;; \
    esac \
    && FILE_VERSION="$(echo "$APP_VERSION" | sed -E 's/^([0-9]+)\.([0-9]+)\.([0-9]+).*/\1.\2.\3.0/')" \
    && dotnet publish src/scanner.net/scanner.net.csproj -c Release -r "$RID" --self-contained true \
    /p:PublishSingleFile=true \
    /p:PublishTrimmed=false \
    /p:Version="$APP_VERSION" \
    /p:InformationalVersion="$APP_VERSION_RAW" \
    /p:AssemblyVersion="$FILE_VERSION" \
    /p:FileVersion="$FILE_VERSION" \
    -o /app/out

FROM debian:bookworm-slim
WORKDIR /app

RUN apt-get update -q \
    && apt-get install -q -y --no-install-recommends \
    pdftk ca-certificates \
    && apt-get clean \
    && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/out/scanner-net /app/scanner-net
COPY --from=brother-build /src/build/brother-scan-cli /app/brother-scan-cli
COPY src/scanner.net/appsettings.json /app/appsettings.json
COPY entrypoint.sh /app/entrypoint.sh
COPY scan-hook.sh /app/scan-hook.sh
RUN chmod +x /app/scanner-net /app/brother-scan-cli /app/entrypoint.sh /app/scan-hook.sh

VOLUME ["/output/scan"]
EXPOSE 54925/udp

ENV SCANNER_IP=192.168.1.167
ENV BIND_IP=0.0.0.0
ENV BIND_PORT=54925
ENV ADVERTISE_IP=0.0.0.0
ENV ADVERTISE_PORT=54925
ENV CONFIG_INTERVAL_SECONDS=600
ENV OUTPUT_DIR=/output/scan
ENV TEMP_DIR=/tmp/scan

ENTRYPOINT ["/app/entrypoint.sh"]
