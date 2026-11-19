# Vehicle Explorer

Pick a car make and a model year, then browse the vehicle types and models that match — powered by the
[NHTSA vPIC API](https://vpic.nhtsa.dot.gov/api/).

- **Backend:** ASP.NET Core 9 Web API (controllers), typed `HttpClient` + standard resilience handler, in-memory caching
- **Frontend:** Angular 20 (standalone components, signals, zoneless)
- **Packaging:** one Docker image — the Angular build is served as static files by the API
- **Live demo:** `<add your AWS URL here>`

```
Browser ──► .NET API (/api/*) ──► NHTSA vPIC
   ▲            │
   └── Angular static files (wwwroot) served by the same container
```

## API

| Method | Route | Description |
|---|---|---|
| GET | `/api/makes?search=hon&limit=20` | Search makes by name (prefix matches first). The ~12k makes are cached and filtered server-side. |
| GET | `/api/makes/{makeId}/vehicle-types` | Vehicle types for a make |
| GET | `/api/makes/{makeId}/models?year=2015&vehicleType=Passenger%20Car` | Models for make + year, optionally filtered by type |
| GET | `/health` | Health check |

Errors use RFC 7807 problem details: `400` for invalid input (e.g. year outside 1981–next year), `502` when vPIC is unreachable.
In Development the OpenAPI document is at `/openapi/v1.json`.

## Run locally

### Option A — Docker (recommended, only Docker needed)

1. Install [Docker Desktop](https://www.docker.com/products/docker-desktop/) and make sure it is running.
2. Clone and start:
   ```bash
   git clone https://github.com/RubaAbuArja000/vpic-vehicle-explorer.git
   cd vpic-vehicle-explorer
   docker compose up --build
   ```
3. Open <http://localhost:8080>.

Stop with `Ctrl+C` (or `docker compose down`).

### Option B — Without Docker (for development)

Prerequisites: [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) and [Node.js 22+](https://nodejs.org/).

1. Start the API (terminal 1):
   ```bash
   dotnet run --project src/VehicleExplorer.Api --urls http://localhost:5280
   ```
2. Start the Angular dev server (terminal 2) — it proxies `/api` to the API via `client/proxy.conf.json`:
   ```bash
   cd client
   npm ci
   npm start
   ```
3. Open <http://localhost:4200>.

## Tests

```bash
dotnet test                                                   # API: service + HTTP-level tests
cd client && npx ng test --watch=false --browsers=ChromeHeadless   # client unit tests
```

CI (`.github/workflows/ci.yml`) runs both on every push/PR, then builds the Docker image and pushes it to
GitHub Container Registry on `main`.

## Deploying to AWS

See [docs/DEPLOY-AWS.md](docs/DEPLOY-AWS.md) — a single Free Tier EC2 instance running the container. No AWS
credentials are stored in this repository.

## Project layout

```
src/VehicleExplorer.Api/        .NET Web API
  Controllers/MakesController   HTTP endpoints + input validation
  Services/VehicleService       caching, de-duplication, sorting, mapping to DTOs
  Vpic/VpicClient               typed HttpClient for vPIC (raw upstream shapes stay here)
tests/VehicleExplorer.Api.Tests xUnit tests (fake HTTP handler, WebApplicationFactory)
client/                         Angular app
  src/app/make-picker           type-ahead combobox for makes
  src/app/vehicle-api.service   HTTP calls to our API
  src/app/load-state            idle/loading/ok/error helper for async UI state
deploy/ec2-user-data.sh         EC2 bootstrap script
```

See [DECISIONS.md](DECISIONS.md) for design decisions, trade-offs and next steps.
