# Troubleshooting

## `dotnet ef`: "ConnectionStrings__Default environment variable is not set"

`dotnet ef` builds its `DbContext` through the design-time factory (`Backend/src/CEMS.Infrastructure/Persistence/ApplicationDbContextFactory.cs`), which reads the connection string **only from the `ConnectionStrings__Default` environment variable**. It does not read `dotnet user-secrets`, which is what the running API uses. Either export the variable once in your shell:

```bash
# bash / zsh
export ConnectionStrings__Default="Host=localhost;Port=5432;Database=cems;Username=postgres;Password=YOUR_PASSWORD"
```

```powershell
# PowerShell
$env:ConnectionStrings__Default = "Host=localhost;Port=5432;Database=cems;Username=postgres;Password=YOUR_PASSWORD"
```

or pass it per command with `--connection "<connection string>"`.

With the Docker Compose database, the connection string is `Host=localhost;Port=5432;Database=cems;Username=cems;Password=cems_dev_password` (see `.env.example`).

The simplest way to avoid `dotnet ef` entirely is the Compose `app` profile, which applies migrations on startup (`Database__MigrateOnStartup=true`).

## `dotnet ef`: "Unable to retrieve project metadata. Ensure it's an SDK-style project."

The `--project` / `--startup-project` paths are relative to the current directory. Run the command from the repository root (the folder that contains `Backend/` and `Frontend/`).

## Build fails with `MSB3027` / "file is locked by CEMS.Api"

A running API holds its Debug DLLs open, so building Debug again cannot overwrite them. Stop the API first, or build another configuration (`dotnet ef ... --configuration Release`, `dotnet test -c Release`).

## Port 5432 is already in use (Docker Compose)

Another PostgreSQL is already listening on the host's 5432. Publish the container on a different host port by setting `POSTGRES_PORT` in `.env` (for example `POSTGRES_PORT=5433`) and use that port in your connection string.

## The migration fails with an exclusion/unique constraint error

Two migrations add `EXCLUDE USING gist` and unique constraints. On a database that already contains rows they forbid (two ordinary sessions overlapping in one room or for one teacher, overlapping payroll periods, duplicate live enrollments) the migration stops and names the constraint. Resolve those rows, then re-run it. A fresh or demo database is unaffected.

## `CEMS.Postgres.Tests` fail with "PostgreSQL integration tests need a PostgreSQL server"

That project needs a PostgreSQL server whose role may `CREATE DATABASE`, and it fails on purpose rather than skipping. Point `CEMS_TEST_POSTGRES` at one (for example the Compose database: `Host=localhost;Username=cems;Password=cems_dev_password`), or run only the fast suites:

```bash
dotnet test Backend/CEMS.sln --filter "FullyQualifiedName!~CEMS.Postgres.Tests"
```

## The frontend can't reach the API / a certificate warning appears

The frontend talks to `http://localhost:5272/api` by default (`Frontend/src/shared/api/client.ts`, `Frontend/.env.example`), which is where plain `dotnet run --project Backend/src/CEMS.Api` and the Docker Compose `app` profile both listen. To use the HTTPS profile instead (`dotnet run --launch-profile https`, `https://localhost:7099`), set `VITE_API_URL=https://localhost:7099/api` in `Frontend/.env.local`, and trust the development certificate once with `dotnet dev-certs https --trust` so the browser accepts it.

If the browser reports a CORS error, the page's origin is not in `Cors:AllowedOrigins` (default `http://localhost:5173`). Add it with the `Cors__AllowedOrigins__0` environment variable (`CORS_ALLOWED_ORIGIN` for Docker Compose).

## Windows: `dotnet ef` is blocked by Smart App Control / an "Application Control policy" error

This was reported on a Windows machine in an earlier version of this project's README; it has not been reproduced since, so treat the steps below as untested advice. Smart App Control can block `dotnet-ef`'s reflection-based loading of a freshly built local DLL; ordinary process execution of the app is unaffected. Things to try, in order:

1. Run the migration with `--project Backend/src/CEMS.Infrastructure` only (no `--startup-project`), so `CEMS.Api.dll` is never loaded. The design-time factory lives in `CEMS.Infrastructure`, so it should not need the API assembly (the `ConnectionStrings__Default` variable above is still required).
2. Use the Docker Compose `app` profile, or run the API once with `Database__MigrateOnStartup=true` set, which migrates without `dotnet ef`.
