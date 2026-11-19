# Design decisions

## Architecture

**The browser only talks to our .NET API; the API talks to vPIC.** Calling vPIC directly from Angular would have
been less code, but going through the backend lets us cache, normalise and validate in one place, keeps the
public contract stable if vPIC changes, and avoids depending on vPIC's CORS behaviour.

**One container serves both UI and API.** The Angular build is copied into the API's `wwwroot` and served with
`UseStaticFiles` + an SPA fallback. One image, one port, no CORS, and the cheapest possible hosting (one small
EC2 instance). Trade-off: UI and API scale and deploy together. Unknown `/api/*` routes return a real 404
instead of falling through to `index.html`.

**Three layers in the API, kept deliberately thin:**
- `VpicClient` — typed `HttpClient`: builds URLs, deserialises. vPIC's naming (`Make_ID`, `Model_Name`) stays
  inside the `Vpic` namespace.
- `VehicleService` — caching, de-duplication, sorting, mapping to our DTOs.
- `MakesController` — routing and input validation only.

No repository/CQRS/MediatR layers — there is no database and only three read endpoints, so they would be ceremony.

## API design

- `GET /api/makes?search=&limit=` — vPIC returns **~12,000 makes (~1 MB)**. Shipping that to the browser on every
  page load is wasteful, so the full list is cached on the server and filtered there. Prefix matches rank above
  "contains" matches so typing `hon` shows *HONDA* before *SUNDIRO HONDA…*.
- `GET /api/makes/{id}/vehicle-types` and `GET /api/makes/{id}/models?year=&vehicleType=` — resource-style
  routes nested under the make, mirroring how the user drills down.
- Vehicle types are passed by **name** (e.g. `Passenger Car`), because that is what vPIC's endpoint accepts.
  Names contain spaces and parentheses (`Multipurpose Passenger Vehicle (MPV)`), so they are URL-encoded — there's a
  test for it.
- **Errors are RFC 7807 problem details.** Invalid input → `400`; vPIC unreachable → `502 Bad Gateway`
  (it's an upstream failure, not our bug, so not a `500`). The Angular client shows the `detail` message.

## Caching & resilience

- `IMemoryCache`: makes for 24 h, types/models for 6 h (configurable in `appsettings.json`). This data changes
  rarely, and vPIC is a free public service, so being a polite client matters.
- **Failures are never cached** (so a short outage doesn't stick); **empty results are** (no models for a year is
  a valid, stable answer).
- `AddStandardResilienceHandler()` (Polly) gives retries with jittered backoff, timeouts and a circuit breaker.

## Frontend

- Angular 20 standalone components with **signals** and **zoneless** change detection — the current Angular
  direction, and less magic to explain than Zone.js.
- Every request is modelled as `LoadState<T>` (`idle | loading | ok | error`), so each section has explicit
  loading skeletons, empty states and errors with a *Try again* button.
- RxJS only where it adds value: `debounceTime` + `switchMap` for the type-ahead (drops stale responses when
  typing fast), and `switchMap` when make/year/type change.
- The make picker follows the WAI-ARIA combobox pattern (keyboard navigation, `aria-activedescendant`).
- Selecting a type chip calls vPIC's *make + year + type* endpoint rather than filtering client-side, because the
  plain *make + year* endpoint doesn't return a vehicle type per model.

## Assumptions

- "Model year" range is **1981 → next year**. 1981 is when the 17-character VIN standard began; next year's models
  are usually already published. Validated on both client and server.
- Vehicle types are shown **for the make** (vPIC has no "types for make + year" endpoint). Picking a type that has
  no models in the chosen year shows a clear empty state.
- Duplicate models (same `Model_ID`) returned by vPIC are collapsed.
- No authentication — the data is public and read-only.

## Trade-offs

- **In-memory cache** is per instance and lost on restart. Fine for one instance; with several, use Redis
  (`IDistributedCache`) or put a CDN in front of the GET endpoints.
- **No cache-stampede protection:** two concurrent first requests can both call vPIC. Harmless at this scale;
  `HybridCache` (.NET 9) would fix it.
- **EC2 over managed container services:** App Runner/ECS Fargate are nicer to operate but not Free Tier.
  Cost: manual redeploy (`sudo /usr/local/bin/redeploy`) and plain HTTP unless CloudFront is added.
- No URL state yet — refreshing the page loses the selection.

## Next steps

1. Sync make/year/type to the URL query string so searches are shareable and survive refresh.
2. HTTPS via CloudFront in front of EC2, and automatic redeploy from CI (SSM Run Command via GitHub OIDC — no
   long-lived keys).
3. Swap `IMemoryCache` for `HybridCache`; add `Cache-Control` headers on API responses.
4. Component tests for the make picker (keyboard navigation) and an end-to-end Playwright smoke test.
5. Structured logging + request metrics (OpenTelemetry) to see vPIC latency and cache hit rate.
6. Infrastructure as code (Terraform/CDK) for the EC2 instance and security group.

## How I used AI

I used Claude (Claude Code) as a pair programmer: to scaffold the projects, draft the vPIC client/service/
controller, write the first version of the tests, the Dockerfile, the CI workflow and these docs. I checked the
real vPIC responses with `curl` before writing any DTOs, reviewed every file, ran the app in a browser, and kept
the tests as the referee.

**Things it got wrong that I fixed:**

1. **Validation errors came back with the wrong content type.** The generated controller had
   `[Produces("application/json")]`. It looks harmless, but it overrides the content type of
   `ValidationProblem(...)` responses, so `400`s were sent as `application/json` instead of
   `application/problem+json`. The HTTP-level test caught it; I removed the attribute and left a comment explaining why.
2. **A test that would have passed for the wrong reason.** The fake HTTP handler matched requests with
   `Uri.ToString()`, which *un-escapes* the URL — so the URL-encoding test for
   `Multipurpose Passenger Vehicle (MPV)` couldn't actually see the encoding. Switched to `Uri.AbsoluteUri`.
3. **Over-clever SPA fallback.** The first draft rewrote the fallback endpoint's route pattern with a regex
   negative look-ahead to exclude `/api`. It worked but nobody could read it; replaced with an explicit
   `app.Map("/api/{**rest}", ...)` 404 plus the normal `MapFallbackToFile`.
4. **Angular tests failed with `NG0908`.** The app is zoneless, but the generated `TestBed` setup assumed Zone.js;
   added `provideZonelessChangeDetection()` to the test providers.
