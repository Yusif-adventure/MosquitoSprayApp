# Smart Mosquito Control

ASP.NET Core MVC (.NET 10) app for controlling IoT mosquito sprayers: manual spray, schedules, device
monitoring, notifications. SQLite + ASP.NET Identity (email/password and optional Google sign-in).

## Run locally

```bash
dotnet run --project SmartMosquitoControl.csproj
```

Open the URL printed in the console. In `Development` the app:

- creates `data/mosquitocontrol.db` and applies migrations automatically,
- **simulates hardware**: a spray completes instantly on the server and the sprayer shows as online
  (`Device:SimulateHardware`),
- does not require email confirmation (`Auth:RequireConfirmedEmail`); confirmation links are written to the log.

> The folder contains both a `.sln` and a `.csproj`, so a bare `dotnet build` is ambiguous. Name the project, or the solution.

```bash
dotnet build SmartMosquitoControl.sln
dotnet test  SmartMosquitoControl.sln
```

## Configuration

Everything is a normal ASP.NET configuration key (appsettings, user secrets, or environment variables with `__`).

| Key | Default | Purpose |
|---|---|---|
| `Data:Directory` | `data` | SQLite file, data-protection keys. Mount a volume here in Docker. |
| `ConnectionStrings:DefaultConnection` | `<Data:Directory>/mosquitocontrol.db` | Override the SQLite connection string. |
| `Auth:RequireConfirmedEmail` | `false` in Development, `true` otherwise | Users must click the emailed link before signing in. |
| `Email:Host`, `Port`, `User`, `Password`, `From`, `EnableSsl` | unset | SMTP. If unset, emails are only written to the log. |
| `Authentication:Google:ClientId` / `ClientSecret` | unset | Enables "Continue with Google". Hidden when unset. |
| `Device:SimulateHardware` | `true` in Development, `false` otherwise | Complete sprays on the server instead of waiting for a sprayer. |
| `Scheduler:Enabled` | `true` | Run the background service that fires schedules. |
| `Https:Redirect` | `true`, `false` inside a container | Redirect HTTP to HTTPS. |
| `AllowedHosts` | `*` | Restrict to your hostname(s) in production. |

Set secrets with User Secrets for development, never in committed files:

```bash
dotnet user-secrets set "Authentication:Google:ClientId" "..."
dotnet user-secrets set "Authentication:Google:ClientSecret" "..."
```

## Docker

```bash
cp .env.example .env      # edit it
docker compose up --build
```

The app listens on 8080 inside the container (mapped to 5253). Database, keys and logs live on named volumes.
Behind a TLS-terminating proxy, set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`. See `PRODUCTION_SETUP.md`.

## How spraying works

1. **Pair a sprayer** (Settings → Linked Devices). You get a one-time **device key**; only its hash is stored.
   A sprayer ID can belong to only one account.
2. **Spray Now** or a due **schedule** queues a `DeviceCommand` for the account's *primary* sprayer.
   Manual sprays are queued the moment you tap, not when the countdown ends.
3. The **sprayer polls the device API**, runs the spray, and reports back.
4. A background service checks schedules every 30 seconds. A schedule more than 15 minutes late is
   marked *Missed* instead of firing at a surprising time.

### Device API

All requests carry `X-Device-Id` and `X-Device-Key` headers. Send `Content-Type: application/json` on POSTs (use `{}` for no data).

| Request | Purpose |
|---|---|
| `POST /api/device/heartbeat` `{"insecticideLevel": 0-100}` | Mark the sprayer online; optionally report its level. `204`. |
| `GET /api/device/commands/next` | `200 {"id","type":"spray","durationSeconds"}` or `204` if idle. Poll every few seconds. |
| `POST /api/device/commands/{id}/complete` `{"success": true, "insecticideLevel": 55}` | Report the outcome. `204`, or `404` if the command isn't in progress. |

A sprayer that hasn't contacted the server for 2 minutes shows as offline. A command nobody finishes within 5 minutes expires.

## Layout

```
Controllers/   Home, Account, Schedule, Devices (web UI) and DeviceApi (sprayers)
Services/      MosquitoDataService (per-user data), DeviceCommandService, ScheduleProcessor, auth, email
Data/          EF Core context + migrations (SQLite)
tests/         xUnit tests (in-memory SQLite)
```
