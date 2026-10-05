# Production setup

## Checklist

1. **Secrets** – supply them as environment variables / a secret store, never in committed files:
   `Authentication__Google__ClientId`, `Authentication__Google__ClientSecret`, `Email__*`.
2. **Email** – configure `Email__Host` and `Email__From`. Production requires email confirmation by default,
   and without SMTP the confirmation links only appear in the log (so nobody can sign in).
   Temporary escape hatch: `Auth__RequireConfirmedEmail=false`.
3. **Persistence** – mount volumes on `/app/data` (database + data-protection keys) and, if you want to keep them, `/app/logs`.
   Losing `/app/data/keys` signs everyone out; losing the database loses everything. Back up `/app/data`.
4. **HTTPS** – terminate TLS at a reverse proxy and set `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`
   so the app sees the real scheme and client IP (cookies, emailed links, rate limiting).
5. **Hosts** – set `AllowedHosts` to your domain(s).
6. **Google sign-in** – add `https://<your-domain>/signin-google` as an authorised redirect URI.
7. **Sprayers** – set `Device__SimulateHardware=false` (the default outside Development) and configure real sprayers
   to use the device API described in `README.md`.

## Single instance only

The app uses SQLite and an in-process scheduler, so run **one** replica. To scale out, move to a server database
(regenerate migrations for that provider) and run the scheduler in a single worker.

## Upgrading an existing database

The `DeviceCommandsAndScheduler` migration:

- adds the command queue, per-device keys and schedule status,
- makes sprayer IDs globally unique (the migration fails if two accounts already share an ID — fix those rows first),
- drops `Devices.IsOnline`, `Schedules.Description` and `AspNetUsers.PreferredDevice`.

Sprayers paired before this migration have no key; unlink and re-pair them to get one.
Past schedules still marked pending are marked *Missed* the first time the scheduler runs.
The development database moved from `./mosquitocontrol.db` to `./data/mosquitocontrol.db`; set
`ConnectionStrings__DefaultConnection` to keep using the old file.
