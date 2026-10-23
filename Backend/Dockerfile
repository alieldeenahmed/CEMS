# Builds the CEMS API image. Build context is the Backend/ folder (see docker-compose.yml).
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Restore first, from the project files alone, so source edits don't invalidate the package layer.
COPY src/CEMS.Domain/CEMS.Domain.csproj src/CEMS.Domain/
COPY src/CEMS.Application/CEMS.Application.csproj src/CEMS.Application/
COPY src/CEMS.Infrastructure/CEMS.Infrastructure.csproj src/CEMS.Infrastructure/
COPY src/CEMS.Api/CEMS.Api.csproj src/CEMS.Api/
RUN dotnet restore src/CEMS.Api/CEMS.Api.csproj

COPY src ./src
RUN dotnet publish src/CEMS.Api/CEMS.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0
# QuestPDF (pay stubs, report cards, dashboard PDF) needs fontconfig on Linux.
RUN apt-get update \
    && apt-get install -y --no-install-recommends libfontconfig1 \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "CEMS.Api.dll"]
