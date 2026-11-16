# syntax=docker/dockerfile:1

# The .NET and web builds run on the build platform and give the same output for every target
FROM --platform=$BUILDPLATFORM ghcr.io/jdx/mise:2026.11.8 AS mise-build
FROM ghcr.io/jdx/mise:2026.11.8 AS mise

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0 AS web
COPY --from=mise-build /usr/local/bin/mise /usr/local/bin/mise
WORKDIR /src
COPY mise.toml mise.lock ./
RUN mise trust && mise install --locked bun
COPY web/package.json web/bun.lock web/bunfig.toml web/
RUN mise exec -- bun install --frozen-lockfile --cwd web
COPY web/ web/
# The release notes mark the release of this version as current
ARG VERSION=unreleased
RUN VITE_VERSION="$VERSION" mise exec -- bun run --cwd web build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:11.0 AS publish
WORKDIR /src
COPY . .
COPY --from=web /src/src/YGrab.Host/wwwroot src/YGrab.Host/wwwroot
RUN --mount=type=cache,target=/root/.nuget/packages \
  dotnet publish src/YGrab.Host --configuration Release --output /app -p:RestoreLockedMode=true

FROM mcr.microsoft.com/dotnet/sdk:11.0 AS tools
RUN apt-get update \
  && apt-get install --yes --no-install-recommends llvm \
  && rm -rf /var/lib/apt/lists/*
COPY --from=mise /usr/local/bin/mise /usr/local/bin/mise
WORKDIR /src
COPY mise.toml mise.lock ./
RUN mise trust && mise install --locked deno uv yt-dlp \
  && mkdir /opt/tools \
  && cp "$(mise which deno)" /opt/tools/ \
  && cp -r "$(mise where yt-dlp)" /opt/tools/yt-dlp
COPY Directory.Packages.props src/YGrab.Infrastructure.Python/YGrabPythonServiceCollectionExtensions.cs ./
COPY src/YGrab.Infrastructure.Python/Python/requirements.txt ./
# Stages the Python that the host expects, so it finds it and downloads nothing
# CSnakes.Stage targets an older .NET, hence the roll forward
ENV CSNAKES_REDIST_CACHE=/opt/python
RUN csnakes="$(sed -n 's/.*"CSnakes.Runtime" Version="\(.*\)".*/\1/p' Directory.Packages.props)" \
  && python="$(sed -n 's/.*PythonVersion = "\(.*\)";/\1/p' YGrabPythonServiceCollectionExtensions.cs)" \
  && dotnet tool install CSnakes.Stage --version "$csnakes" --tool-path /usr/local/bin \
  && DOTNET_ROLL_FORWARD=Major setup-python --python "$python" --venv /opt/python/venv \
  && cp "$(mise which uv)" /opt/python/venv/bin/ \
  && VIRTUAL_ENV=/opt/python/venv /opt/python/venv/bin/uv pip install \
  --no-cache --compile-bytecode --requirements requirements.txt
# Drops the build files, the tests and the debug symbols of Python
# GNU strip breaks the x64 python executable and eu-strip skips the arm64 one
RUN rm -rf /opt/python/CSnakes/*/python/build /opt/python/CSnakes/*/python/install/lib/python*/test \
  && find /opt/python -name '*.a' -delete \
  && find /opt/python/CSnakes -type f -name '*.so*' -exec llvm-strip --strip-debug {} + \
  && llvm-strip --strip-debug /opt/python/CSnakes/*/python/install/bin/python3.[0-9][0-9]

FROM mcr.microsoft.com/dotnet/aspnet:11.0
RUN apt-get update \
  && apt-get install --yes --no-install-recommends curl \
  && rm -rf /var/lib/apt/lists/*
COPY --from=mwader/static-ffmpeg:9.0.2-1 /ffmpeg /ffprobe /usr/local/bin/
COPY --from=tools /opt/tools/deno /usr/local/bin/
COPY --from=tools /opt/tools/yt-dlp /opt/yt-dlp
COPY --from=tools /opt/python /opt/python
COPY --from=publish /app /app
RUN mkdir /data && chown "$APP_UID:$APP_UID" /data
ENV PATH=/opt/yt-dlp:$PATH \
  CSNAKES_REDIST_CACHE=/opt/python \
  DENO_NO_UPDATE_CHECK=1 \
  Python__VirtualEnvironment=/opt/python/venv \
  Storage__DataDirectory=/data
WORKDIR /app
USER $APP_UID
VOLUME /data
EXPOSE 8080
HEALTHCHECK CMD curl --fail --silent --output /dev/null "http://127.0.0.1:$ASPNETCORE_HTTP_PORTS/api/health" || exit 1
ENTRYPOINT ["dotnet", "YGrab.Host.dll"]
