# BobsBookstoreClassic .NET 10 Migration Summary

## Status

**Build result: SUCCEEDED — 0 errors, 23 warnings (all NuGet vulnerability advisories)**

## What was migrated

### Project files
| Project | Before | After |
|---------|--------|-------|
| Bookstore.Web | .NET Framework 4.8, old-style csproj | net10.0, SDK-style `Microsoft.NET.Sdk.Web` |
| Bookstore.Data | netstandard2.0 (EF6), old-style csproj | net10.0, SDK-style |
| Bookstore.Domain | netstandard2.0, old-style csproj | net10.0, SDK-style |
| Bookstore.Common | netstandard2.0 | net10.0 |

### Application entry point
- Removed: `Global.asax`, `Global.asax.cs`, `Startup.cs` (OWIN), all `App_Start/` files (`AuthenticationSetup.cs`, `BundleConfig.cs`, `ConfigurationSetup.cs`, `DependencyInjectionSetup.cs`, `FilterConfig.cs`, `LoggingSetup.cs`, `RouteConfig.cs`)
- Added: `Bookstore.Web/Program.cs` — single ASP.NET Core entry point with full middleware pipeline, Autofac DI via `AutofacServiceProviderFactory`, cookie + optional OpenIdConnect authentication, route configuration

### Configuration
- Removed: `Web.config`
- Added: `Bookstore.Web/appsettings.json` — JSON equivalent of appSettings and connectionStrings
- `BookstoreConfiguration.cs` — replaced `System.Configuration.ConfigurationManager` with `IConfiguration`

### Entity Framework
- Migrated EF6 → EF Core 10.0
- `ApplicationDbContext`: constructor updated from `(string connectionString)` to `(DbContextOptions<ApplicationDbContext> options)`, `OnModelCreating` converted from EF6 conventions to EF Core Fluent API (explicit `ToTable()`, `HasOne().WithMany().HasForeignKey().OnDelete()`, `ValueGeneratedOnAdd()`)
- `BookstoreDbInitializer`: replaced EF6 `DropCreateDatabaseIfModelChanges<T>` with static class using EF Core `HasData()` seeding pattern
- All repositories: `System.Data.Entity` → `Microsoft.EntityFrameworkCore`, string-based `Include("Genre")` → lambda `Include(x => x.Genre)`, EF6 nested includes → `Include().ThenInclude()` chains, `Task.Run(() => Add())` → `AddAsync()`

### ASP.NET Core migration
- All controllers: `System.Web.Mvc` → `Microsoft.AspNetCore.Mvc`, `ActionResult` → `IActionResult`
- `[RouteArea("Admin")]` → `[Area("Admin")]` on `AdminAreaControllerBase`
- `AdminAreaRegistration.cs` → stub (Admin area registered via `[Area]` attribute and route convention in `Program.cs`)
- All ViewModels with `SelectList` properties: `System.Web.Mvc` → `Microsoft.AspNetCore.Mvc.Rendering`
- `InventoryCreateUpdateViewModel`, `ResaleCreateViewModel`: `HttpPostedFileBase` → `IFormFile`
- `ImageTypesAttribute`, `MaxFileSizeAttribute`: `HttpPostedFileBase` → `IFormFile`

### Middleware / authentication
- `LocalAuthenticationMiddleware`: OWIN `OwinMiddleware` → ASP.NET Core `IMiddleware`, `IOwinContext` → `HttpContext`
- `HttpContextExtensions`: `HttpContextBase`/`HttpCookie` → ASP.NET Core `HttpContext`/`CookieOptions`
- `AuthenticationController`: cookie deletion via `HttpContext.SignOutAsync()`, URL construction from `Request.Scheme`/`Request.Host`
- `IOwinRequestExtensions`: replaced with comment stub (no longer needed)

### Razor views
- `_ViewImports.cshtml`: added `@using Microsoft.AspNetCore.Routing` (for `RouteValueDictionary`)
- `Areas/Admin/Views/_ViewImports.cshtml`: new file with `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`
- `EnumDropDownListFor` → `DropDownListFor` with `Html.GetEnumSelectList<T>()` in Orders/Index.cshtml and Offers/Index.cshtml

### NuGet packages
- `EntityFramework 6.x` → `Microsoft.EntityFrameworkCore 10.0.0` + `.SqlServer` + `.Design`
- `Autofac.Mvc5` / `Autofac.Owin` → `Autofac.Extensions.DependencyInjection 10.0.0`
- `NLog.AWS.Logger` (wrong package) → `AWS.Logger.NLog 3.3.4` (correct package; namespace `NLog.AWS.Logger`, class `AWSTarget`)
- `Magick.NET-Q8-AnyCPU` upgraded 14.6.0 → 14.17.2 (reduced vulnerability advisories from 668 to ~1)
- Added `System.Security.Cryptography.Xml 10.0.0` pin to reduce transitive vulnerability warnings
- Added `<GenerateAssemblyInfo>false</GenerateAssemblyInfo>` to all three projects (prevents CS0579 duplicate attribute errors from manual `Properties/AssemblyInfo.cs` files)

## Remaining warnings (non-blocking)

### NuGet vulnerability advisories (23 total)
1. **GHSA-w3x6-4m5h-cxqf** — `System.Security.Cryptography.Xml` high severity advisory persists even at version 10.0.0. This is a transitive dependency pulled in by `Magick.NET`. Unresolvable without removing Magick.NET or waiting for an upstream fix. Monitor Magick.NET releases.

2. **MVC1000** — `Html.RenderPartial()` calls in address view may cause deadlocks under async context. Affected file: `Views/Address/CreateUpdate.cshtml` (or similar partial rendering). Replace `@Html.RenderPartial("_SomePartial")` with `<partial name="_SomePartial" />` tag helper.

## Next steps

### Required before production deployment

1. **EF Core database migrations** — EF6 migrations are incompatible with EF Core. Run:
   ```
   dotnet ef migrations add InitialCreate --project app/Bookstore.Data --startup-project app/Bookstore.Web
   dotnet ef database update --project app/Bookstore.Data --startup-project app/Bookstore.Web
   ```
   Review generated migration against existing schema to ensure no data loss.

2. **End-to-end testing** — The application has not been run against a real database. Test all critical paths:
   - User registration / login (local auth and AWS/OpenIdConnect paths)
   - Book browsing, search, shopping cart, checkout
   - Admin dashboard: inventory, orders, offers, reference data
   - Resale/wishlist flows

3. **Static files** — The OWIN `BundleConfig` (CSS/JS bundles) was removed. Verify that all static assets load correctly. Consider replacing with a bundler (e.g., `dotnet-bundle`, `libman`, or a Node build step).

4. **Session configuration** — If any controllers relied on `Session` state, verify `builder.Services.AddSession()` / `app.UseSession()` is configured in `Program.cs`.

5. **MVC1000 RenderPartial warning** — Replace `@{ Html.RenderPartial("..."); }` with `<partial name="..." />` tag helper in affected views to eliminate deadlock risk in async contexts.

6. **Review Autofac registrations** — The `ContainerBuilder` in `Program.cs` registers services based on the original `DependencyInjectionSetup.cs`. Verify all custom registrations match the original behavior, particularly conditional AWS vs. local service registrations.

7. **Connection string** — Update `appsettings.json` (or environment-specific config / AWS SSM Parameter Store) with the real database connection string before deploying.

### Optional improvements

- Replace remaining `@Html.Partial()` calls with `<partial>` tag helpers throughout all views for consistency and deadlock safety.
- Upgrade `System.ComponentModel.Annotations 5.0.0` in Bookstore.Data to a current version (it is a legacy package; the types are in `System.ComponentModel.Annotations` in .NET 10 BCL).
- Add health check endpoint via `app.MapHealthChecks("/health")`.
- Enable response compression and HTTPS redirection in `Program.cs` if not already handled by the load balancer.
