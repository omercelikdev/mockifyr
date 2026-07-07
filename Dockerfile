# Mockifyr — single image that serves the mock engine + admin API and the embedded dashboard.
# The dashboard is reachable at /__mockifyr; every other path is the mock-serving surface.

# ---- Stage 1: build the dashboard (embedded base so it serves under /__mockifyr) ----
FROM node:22-alpine AS ui
WORKDIR /ui
RUN corepack enable
COPY ui/package.json ui/pnpm-lock.yaml ./
RUN pnpm install --frozen-lockfile
COPY ui/ ./
RUN pnpm build:embedded

# ---- Stage 2: publish the .NET host ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json nuget.config Directory.Build.props Directory.Packages.props ./
COPY src/ ./src/
RUN dotnet publish src/Mockifyr.Server/Mockifyr.Server.csproj -c Release -o /app

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./
COPY --from=ui /ui/dist ./dashboard
EXPOSE 8080
# --dashboard serves the built UI under /__mockifyr; mount mappings at /work and pass --root-dir to load them.
ENTRYPOINT ["dotnet", "Mockifyr.Server.dll", "--port", "8080", "--dashboard", "/app/dashboard"]
