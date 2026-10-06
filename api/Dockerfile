FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY Directory.Build.props Directory.Packages.props global.json CodePath.sln ./
COPY src/Api/CodePath.Api/CodePath.Api.csproj src/Api/CodePath.Api/
COPY src/Application/CodePath.Application/CodePath.Application.csproj src/Application/CodePath.Application/
COPY src/Domain/CodePath.Domain/CodePath.Domain.csproj src/Domain/CodePath.Domain/
COPY src/Infrastructure/CodePath.Infrastructure/CodePath.Infrastructure.csproj src/Infrastructure/CodePath.Infrastructure/
COPY src/Shared/CodePath.Shared.Kernel/CodePath.Shared.Kernel.csproj src/Shared/CodePath.Shared.Kernel/
COPY src/Shared/CodePath.Shared.Web/CodePath.Shared.Web.csproj src/Shared/CodePath.Shared.Web/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/Api/CodePath.Api/CodePath.Api.csproj
COPY src/ ./src/
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/Api/CodePath.Api/CodePath.Api.csproj \
    --configuration Release \
    --no-restore \
    --output /app/publish \
    /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish ./

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER app
ENTRYPOINT ["dotnet", "CodePath.Api.dll"]
