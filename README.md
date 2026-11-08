# Karlson

Weekly meal plans grounded in what Tbilisi stores actually sell and what it costs.
This is the product app: an ASP.NET Core API plus a React web app. Store data comes from
the separate [karlson-ingestion](https://github.com/BurnOutDev/karlson-ingestion) service,
which owns the `catalog` schema in the shared Postgres; this app owns the `app` schema and
reads `catalog` through the `karlson_app` role (SELECT only).

## Run locally

Postgres comes from the ingestion repo's `docker-compose.yml`. Then:

```bash
dotnet run --project src/Karlson.Api --launch-profile http
```

```bash
npm --prefix web run dev
```

The API listens on http://localhost:5080 and applies its migrations on start in Development.
Vite serves the UI on http://localhost:5173 and proxies `/api` to the API. `npm --prefix web run build`
writes the production bundle into `src/Karlson.Api/wwwroot`, which the API serves itself.

## Layout

- `src/Karlson.Api/Domain/` - aggregates with behaviour (`Household` with its `Member`s).
- `src/Karlson.Api/Features/<Feature>/` - vertical slices: Minimal API endpoints, request/response
  records and handlers together. Commands load an aggregate, call one method, save. Queries
  project straight to DTOs. No service or repository layer.
- `src/Karlson.Api/Data/AppDbContext.cs` - `app` schema entities plus read-only models over the
  `catalog` schema (excluded from migrations).
- `web/` - Vite + React + TypeScript, Tailwind v4, shadcn/ui, TanStack Query.

## Access

No user accounts yet. A device creates a household or joins one with its six-character invite
code; the API sets a protected cookie with the household id. Everyone with the code is in.
