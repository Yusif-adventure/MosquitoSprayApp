# Smart Mosquito Control

Smart Mosquito Control is an ASP.NET Core MVC application for managing an indoor mosquito control device. It includes a dashboard, schedule management, device connections, monitoring, notifications, and settings.

## Project overview

This app is built with:

- ASP.NET Core MVC
- .NET 9 target framework
- Razor Views for UI
- In-memory service data for demo/mock device behavior

The main application entry point is:

- `Program.cs`

Main app configuration files:

- `appsettings.json`
- `appsettings.Development.json`
- `Properties/launchSettings.json`

## Prerequisites

Before running the project, install the following:

- .NET SDK 9.0 or later
- A modern browser such as Edge, Chrome, or Firefox
- Optional: Visual Studio 2022 or VS Code with C# support

Check your installed SDK:

```bash
dotnet --version
```

This project targets `net10.0` and uses the .NET 10 SDK for build and runtime.

## Installation

From the project directory:

```bash
cd SmartMosquitoControl
```

Restore dependencies:

```bash
dotnet restore
```

## Build

Build the app in Debug mode:

```bash
dotnet build
```

For a release build:

```bash
dotnet build -c Release
```

## Run locally

Start the application:

```bash
dotnet run
```

Or run the project directly with the project file:

```bash
dotnet run --project SmartMosquitoControl.csproj
```

By default, the app will run on:

- HTTP: `http://localhost:5253`
- HTTPS: `https://localhost:7080` (if configured through launch settings)

The local URLs are defined in:

- `Properties/launchSettings.json`

## Production deployment checklist

Before deploying to production, review and update the following configuration points:

### 1) Application URL / ports

File:

- `Properties/launchSettings.json`

These are development-only launch settings. In production, do not rely on these values. Configure the actual public URL via hosting platform settings, environment variables, or reverse proxy configuration.

Relevant items:

- `applicationUrl`
- `ASPNETCORE_ENVIRONMENT`

### 2) General app settings

File:

- `appsettings.json`

This file is the base production configuration. Update any environment-specific settings here or use environment variables in the deployment environment.

Examples to review:

- `AllowedHosts`
- logging configuration
- any future feature settings or connection strings

### 3) Development-only settings

File:

- `appsettings.Development.json`

This file is meant for local development only. It should not contain production values.

### 4) Routing and default app behavior

File:

- `Program.cs`

This file sets up the MVC app and defines the default route:

```csharp
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
```

For production, verify that the app is correctly behind HTTPS and that the hosting platform is configured to terminate TLS properly.

### 5) HTTPS / security settings

File:

- `Program.cs`

Current production-relevant middleware includes:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
```

When moving to production:

- ensure the site is served over HTTPS
- configure HSTS properly on the production host
- confirm the reverse proxy or load balancer forwards HTTPS traffic correctly
- keep the exception page and error routes safe and production-appropriate

## Main app endpoints

The app uses the `HomeController` for page routes.

File:

- `Controllers/HomeController.cs`

### Public pages

| Route                 | Method | Purpose                            |
| --------------------- | ------ | ---------------------------------- |
| `/`                   | GET    | Landing page / home welcome screen |
| `/Home/Index`         | GET    | Home page view                     |
| `/Home/Login`         | GET    | Login screen                       |
| `/Home/Login`         | POST   | Login form submission              |
| `/Home/Dashboard`     | GET    | Main dashboard                     |
| `/Home/ManualSpray`   | GET    | Manual spray controls              |
| `/Home/Schedule`      | GET    | Schedule page                      |
| `/Home/Monitoring`    | GET    | Monitoring and spray history       |
| `/Home/Notifications` | GET    | Notification listing               |
| `/Home/Profile`       | GET    | User profile and settings          |
| `/Home/LinkedDevices` | GET    | Linked device list                 |
| `/Home/Error`         | GET    | Error page                         |

### Schedule actions

| Route                  | Method | Purpose                      |
| ---------------------- | ------ | ---------------------------- |
| `/Home/ToggleSchedule` | POST   | Enable or disable a schedule |
| `/Home/DeleteSchedule` | POST   | Remove a schedule            |
| `/Home/AddSchedule`    | POST   | Add a new schedule           |

### Device actions

| Route                | Method | Purpose                      |
| -------------------- | ------ | ---------------------------- |
| `/Home/LinkDevice`   | POST   | Link a new sprayer/device    |
| `/Home/UnlinkDevice` | POST   | Unlink a device              |
| `/Home/SprayNow`     | POST   | Trigger a manual spray cycle |

### Settings actions

| Route                | Method | Purpose            |
| -------------------- | ------ | ------------------ |
| `/Home/SaveSettings` | POST   | Save user settings |
| `/Home/Logout`       | POST   | Logout action      |

## Notes for production

When the project is prepared for deployment, make sure the following are updated before pushing to production:

1. Replace localhost URLs and development profile settings in `Properties/launchSettings.json`.
2. Move secrets and environment-specific values out of source-controlled files and into environment variables or secure configuration stores.
3. Confirm `Program.cs` is using the correct production environment behavior.
4. Validate all routing paths under `Controllers/HomeController.cs` against the production site structure and hosting provider.
5. Check the app runs correctly from the deployed domain with HTTPS enabled.
6. Review static asset paths and ensure all branding/images are correctly served from the deployed public site.

## Useful commands

```bash
dotnet restore
dotnet build
dotnet run
dotnet run --project SmartMosquitoControl.csproj
dotnet build -c Release
```

## Troubleshooting

### App does not start

- Confirm the project path is correct.
- Run `dotnet restore` first.
- Ensure the .NET SDK is installed and available in PATH.
 - You can also run the app in Docker using the provided `Dockerfile` and `docker-compose.yml`.
 
### Run in Docker (local)

Build and start services:

```bash
docker compose build --pull
docker compose up -d
```

The web app will be available at `http://localhost:5253` after migrations run.
- Check that the project file exists and targets a supported framework.

### Port issues

If the app cannot bind to the default port, verify the port in:

- `Properties/launchSettings.json`

### Production issue

If a configuration value is environment-specific, prefer:

- environment variables
- Azure App Service settings
- container environment variables
- production secret stores

rather than editing development-only values into the app permanently.

## Project structure summary

```text
SmartMosquitoControl/
├── Controllers/
├── Models/
├── Services/
├── Views/
├── wwwroot/
├── appsettings.json
├── appsettings.Development.json
├── Program.cs
├── SmartMosquitoControl.csproj
├── Properties/
└── README.md
```

## Final note

This app is currently configured as a local MVC demo application. For production, the main items to review are the environment configuration, HTTPS setup, base URL settings, and any external device/service integrations that may be added later.
