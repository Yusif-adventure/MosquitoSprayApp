# Production-Ready Setup Guide

## Overview
The MosquitoSprayApp has been transformed from a prototype into a production-ready ASP.NET Core application with the following improvements:

## Changes Made

### 1. **Database Layer (EF Core)**
- ✅ Added Entity Framework Core 10.0.0 for data persistence
- ✅ Created `ApplicationDbContext` with proper relationships
- ✅ All models now support user-specific data isolation
- ✅ Added database indexes for performance optimization
- ✅ Generated initial migration: `Data/Migrations/InitialCreate`

**Files Modified/Created:**
- `SmartMosquitoControl.csproj` - Added EF Core packages
- `Data/ApplicationDbContext.cs` - Database context with relationships
- `Models/` - All models updated with UserId and foreign keys

### 2. **Authentication & Authorization**
- ✅ Implemented ASP.NET Core Identity for user management
- ✅ Created `ApplicationUser` class extending IdentityUser
- ✅ Added `AuthenticationService` for login/register operations
- ✅ Added `[Authorize]` attributes to all protected endpoints
- ✅ Password policy enforced: 8+ chars, uppercase, lowercase, digits, special chars

**Files Created:**
- `Models/ApplicationUser.cs` - Extended identity user
- `Services/AuthenticationService.cs` - Auth service with logging
- `Models/RegisterViewModel.cs` - Registration form with validation

### 3. **Logging Infrastructure**
- ✅ Integrated Serilog for structured logging
- ✅ Console and file-based logging enabled
- ✅ Daily log rotation configured
- ✅ All controller actions log errors and important events

**Configuration:**
- Logs stored in `logs/` directory
- Rolling daily logs with format: `app-YYYYMMDD.txt`

### 4. **Global Error Handling**
- ✅ Created middleware for centralized exception handling
- ✅ Graceful error responses with proper HTTP status codes
- ✅ Try-catch blocks in all controller actions
- ✅ Detailed error logging for troubleshooting

**Files Created:**
- `Middleware/GlobalExceptionHandlingMiddleware.cs` - Error handling middleware

### 5. **Input Validation**
- ✅ Enhanced data annotations on all view models
- ✅ Email validation on login/register
- ✅ Password strength requirements
- ✅ String length constraints
- ✅ Custom error messages for user feedback

**Updated Models:**
- `LoginViewModel` - Added required attributes and error messages
- `RegisterViewModel` - Comprehensive password and email validation
- `SettingsViewModel` - Range validation with descriptive messages

### 6. **Security Improvements**
- ✅ HTTPS redirection enabled
- ✅ HSTS headers in production
- ✅ CSRF protection (ValidateAntiForgeryToken)
- ✅ User password hashing via ASP.NET Core Identity
- ✅ Account lockout after failed login attempts
- ✅ Role-based authorization ready for implementation

## Database Setup

### Initial Setup
```bash
# Navigate to project directory
cd /home/revic/Desktop/MosquitoSprayApp

# Install EF Core tools (first time only)
dotnet tool install --global dotnet-ef

# Restore packages
dotnet restore

# Create database (applies migrations)
dotnet ef database update
```

### Connection String Configuration
**Development** (appsettings.json):
```json
"DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=SmartMosquitoControlDb;Trusted_Connection=true;"
```

**Production** (appsettings.Production.json):
```json
"DefaultConnection": "Server=YOUR_SERVER;Database=SmartMosquitoControlDb;User Id=sa;Password=YOUR_PASSWORD;TrustServerCertificate=True;"
```

Replace `YOUR_SERVER` and `YOUR_PASSWORD` with your production database details.

## Running the Application

### Development
```bash
dotnet run
# Opens at https://localhost:7080
```

### Production
```bash
dotnet build -c Release
dotnet publish -c Release -o ./publish
cd publish
dotnet SmartMosquitoControl.dll --environment Production
```

## First-Time User Setup

1. **Create Admin Account:**
   - Navigate to `/Home/Register`
   - Enter email and strong password
   - Submit form to create account

2. **Login:**
   - Navigate to `/Home/Login`
   - Enter credentials created above
   - Access dashboard

## Important Notes

### Password Requirements
Passwords must contain:
- At least 8 characters
- One uppercase letter (A-Z)
- One lowercase letter (a-z)
- One digit (0-9)
- One special character (!@#$%^&*)

### Logging
- All authentication attempts logged
- All errors logged with full stack traces
- Logs rotate daily
- Check `logs/` folder for troubleshooting

### Database Migrations
When making model changes:
```bash
# Create new migration
dotnet ef migrations add MigrationName

# Apply migrations
dotnet ef database update
```

## Remaining Tasks for Full Production

1. **Email Integration** - Add email confirmation for registration
2. **OAuth Integration** - Complete Google OAuth setup
3. **API Layer** - Create REST API endpoints for mobile apps
4. **Unit Tests** - Add comprehensive test suite
5. **Performance** - Add caching strategy for frequently accessed data
6. **Monitoring** - Integrate application insights or similar
7. **Backup Strategy** - Implement automated database backups
8. **Rate Limiting** - Prevent abuse with rate limiting middleware

## Security Checklist

- [ ] Update `appsettings.Production.json` with real database connection string
- [ ] Configure strong passwords for database accounts
- [ ] Enable HTTPS with valid certificate
- [ ] Configure firewall rules for database access
- [ ] Set up regular database backups
- [ ] Enable logging and monitoring
- [ ] Review and test all authentication flows
- [ ] Deploy secrets using environment variables or key vault
- [ ] Enable Azure SQL auditing (if using Azure)
- [ ] Implement rate limiting for login attempts

## Support & Troubleshooting

### Common Issues

**Database Connection Fails:**
- Verify connection string in appsettings.json
- Ensure SQL Server is running
- Check firewall allows database access

**Migrations Error:**
- Ensure DbContext is properly configured
- Delete `Migrations` folder and regenerate if needed
- Run `dotnet restore` before migrations

**Authentication Issues:**
- Clear browser cookies and cache
- Check User table exists in database
- Verify password complexity requirements

## Files Created/Modified

### New Files
- `Data/ApplicationDbContext.cs`
- `Data/Migrations/` (auto-generated)
- `Services/AuthenticationService.cs`
- `Middleware/GlobalExceptionHandlingMiddleware.cs`
- `Models/ApplicationUser.cs`
- `Models/RegisterViewModel.cs`
- `appsettings.Production.json`

### Modified Files
- `Program.cs` - Complete rewrite with EF Core, Identity, and Serilog
- `Controllers/HomeController.cs` - Added auth service, error handling, authorization
- `Models/DeviceState.cs` - Added UserId and relationships
- `Models/LinkedDevice.cs` - Added UserId and relationships
- `Models/ScheduleItem.cs` - Added UserId and relationships
- `Models/SprayHistoryItem.cs` - Added Id and UserId
- `Models/NotificationItem.cs` - Added UserId and IsRead flag
- `Models/LoginViewModel.cs` - Enhanced validation
- `Models/SettingsViewModel.cs` - Better error messages
- `appsettings.json` - Added connection string and app config
- `SmartMosquitoControl.csproj` - Added NuGet packages

## Next Steps

1. Test the application thoroughly with various user scenarios
2. Set up continuous integration/deployment pipeline
3. Configure production database server
4. Implement email notifications
5. Add comprehensive unit tests
6. Set up monitoring and alerting
7. Create deployment documentation for your team
