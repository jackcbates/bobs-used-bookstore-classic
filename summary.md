# Migration Summary: .NET Framework 4.8 → .NET 10

## Result

`dotnet build BobsBookstoreClassic.sln` → **0 errors, 0 warnings**

---

## Changes Made

### Project Files

| File | Change |
|------|--------|
| `app/Bookstore.Web/Bookstore.Web.csproj` | Replaced old-style MSBuild XML with SDK-style, targeting `net10.0` via `Microsoft.NET.Sdk.Web`. Removed all legacy package references (Autofac.Mvc5, Autofac.Owin, Microsoft.Owin.*, EntityFramework, System.Web.Optimization, WebGrease, Antlr, etc.). Added ASP.NET Core packages. |
| `app/Bookstore.Data/Bookstore.Data.csproj` | Changed target from `netstandard2.0` to `net10.0` (single TFM). Replaced EF6 (`EntityFramework`) with EF Core 9 (`Microsoft.EntityFrameworkCore.SqlServer`). Added `Microsoft.Extensions.Configuration.Abstractions`. Removed `Microsoft.CSharp` (automatically available). Upgraded `Magick.NET-Q8-AnyCPU` from 14.6.0 → 14.17.2 to clear vulnerability advisories. |
| `app/Bookstore.Domain/Bookstore.Domain.csproj` | Added `net10.0` alongside `netstandard2.0` multi-targeting. Removed `Microsoft.CSharp` (automatically available). |
| `app/Bookstore.Common/Bookstore.Common.csproj` | Added `net10.0` alongside `netstandard2.0` multi-targeting. |
| `BobsBookstoreClassic.sln` | Updated Bookstore.Web project type GUID from `{FAE04EC0...}` (old-format web) to `{9A19103F...}` (SDK-style). |
| `app/Bookstore.Cdk/Bookstore.Cdk.csproj` | Updated `Amazon.CDK.Lib` to 2.272.0 and `Constructs` to 10.8.1 to clear NU1901 vulnerability advisory. |

**Note on Bookstore.Data single-TFM decision:** `Bookstore.Data` targets `net10.0` only (not `net10.0;netstandard2.0`) because EF Core 9 does not support `netstandard2.0`. There are no .NET Framework consumers of this library in the solution, so multi-targeting would have caused NU1201 (incompatible project reference) or required dual EF implementations.

---

### EF6 → EF Core Migration (`Bookstore.Data`)

- `ApplicationDbContext`: Rewrote constructor to accept `DbContextOptions<ApplicationDbContext>`. Migrated `OnModelCreating` from EF6's `DbModelBuilder` to EF Core's `ModelBuilder`. Replaced `HasRequired`/`WillCascadeOnDelete` with `HasOne`/`OnDelete(DeleteBehavior.NoAction)`. Replaced `HasDatabaseGeneratedOption(Identity)` with `ValueGeneratedOnAdd()`. Removed `PluralizingTableNameConvention.Remove` (EF Core default matches DbSet property names). Moved `BookstoreDbInitializer.Seed()` data into `modelBuilder.Entity<T>().HasData(...)`. Removed `Database.SetInitializer`.
- `PaginatedList.cs`: Replaced `System.Data.Entity` with `Microsoft.EntityFrameworkCore`.
- All repositories: Replaced `System.Data.Entity` with `Microsoft.EntityFrameworkCore`. Fixed `Include(x => x.Collection.Select(y => y.Nav))` → `Include(x => x.Collection).ThenInclude(y => y.Nav)`. Changed `Task.Run(() => dbContext.Set.Add(...))` → `await dbContext.Set.AddAsync(...)`. Updated methods returning `Task<T>` to `Task<T?>` to match EF Core's nullable-safe `SingleOrDefaultAsync`/`FindAsync`.
- Deleted `BookstoreDbInitializer.cs` (seeding moved to `ApplicationDbContext.OnModelCreating`).
- Deleted `app/Bookstore.Data/Properties/AssemblyInfo.cs` (duplicate attributes, now SDK auto-generated).

### Configuration

- `BookstoreConfiguration.cs`: Replaced `System.Configuration.ConfigurationManager` with a static `Initialize(IConfiguration)` method called from `Program.cs` at startup. Retained the singleton dictionary pattern; added environment variable overrides.
- Created `appsettings.json` and `appsettings.Development.json` from `Web.config` settings.
- Deleted `Web.config`, `Web.Release.config`, `Web.Debug.config`.

### ASP.NET Core Application Entry Point

- Created `Program.cs` as the sole application entry point, replacing `Global.asax`, `Startup.cs`, and all `App_Start/` files.
- `Program.cs` configures: `IConfiguration`→`BookstoreConfiguration`, NLog, ASP.NET Core MVC, EF Core DbContext, domain services, repositories, file/image services, authentication (local cookie or Cognito OIDC), area routing, and `db.Database.EnsureCreated()` at startup.
- Deleted `Global.asax`, `Global.asax.cs`, `Startup.cs`, `App_Start/AuthenticationSetup.cs`, `App_Start/BundleConfig.cs`, `App_Start/ConfigurationSetup.cs`, `App_Start/DependencyInjectionSetup.cs`, `App_Start/FilterConfig.cs`, `App_Start/LoggingSetup.cs`, `App_Start/RouteConfig.cs`.

### MVC & Helpers

- All controllers: Changed `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc`. Return types `ActionResult` → `IActionResult`. `[RouteArea]` → `[Area]` on `AdminAreaControllerBase`.
- `AdminAreaControllerBase.cs`: Replaced `[RouteArea("Admin")]` with `[Area("Admin")]`.
- `AdminAreaRegistration.cs`: Deleted (areas are configured by `[Area]` attribute + `MapControllerRoute` in `Program.cs`).
- `LocalAuthenticationMiddleware.cs`: Replaced OWIN `OwinMiddleware`/`IOwinContext` with ASP.NET Core `IMiddleware`/`HttpContext`.
- `HttpContextExtensions.cs`: Changed from `HttpContextBase` to `HttpContext` (ASP.NET Core). `HttpCookie` → `CookieOptions`.
- `IOwinRequestExtensions.cs`: Replaced `IOwinRequest` extension with `HttpRequest` extension.
- `MvcHelpers.cs`: Changed `HtmlHelper` → `IHtmlHelper`, `SelectListItem` → `Microsoft.AspNetCore.Mvc.Rendering.SelectListItem`.
- `ControllerExtensions.cs`: Updated `using` to `Microsoft.AspNetCore.Mvc`.
- `ClaimsPrincipalExtensions.cs`: Updated return types to nullable.
- `ImageTypesAttribute.cs`, `MaxFileSizeAttribute.cs`: `HttpPostedFileBase` → `IFormFile`, `file.ContentLength` → `file.Length`, `file.InputStream` → `file.OpenReadStream()`.
- All models using `SelectListItem`: `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc.Rendering`.
- `InventoryCreateUpdateViewModel.cs`: `HttpPostedFileBase` → `IFormFile`.
- `ReferenceDataCreateViewModel.cs`: `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc.Rendering`.

### Views

- `Areas/Admin/Views/Orders/Index.cshtml`: Replaced `@Html.EnumDropDownListFor` with `@Html.DropDownListFor` + `Html.GetEnumSelectList<T>()` (ASP.NET Core equivalent).
- `Areas/Admin/Views/Offers/Index.cshtml`: Same fix.
- Deleted `Views/Web.config` and `Areas/Admin/Views/web.config` (not used in ASP.NET Core Razor).

### Domain Interfaces (Nullable Return Types)

Updated `IOfferRepository.GetAsync`, `IOrderRepository.GetAsync` (both overloads), `ICustomerRepository.GetAsync` (both overloads), `IAddressRepository.GetAsync`, `IReferenceDataRepository.GetAsync` to return `Task<T?>` to match EF Core's nullable-safe API.

### Deleted Legacy Files

- `app/Bookstore.Web/packages.config`
- `app/Bookstore.Web/Properties/AssemblyInfo.cs`
- `app/Bookstore.Data/Properties/AssemblyInfo.cs`
- `app/Bookstore.Domain/Properties/AssemblyInfo.cs`

### CDK Project (Bookstore.Cdk)

- Updated `Amazon.CDK.Lib` 2.188.0 → 2.272.0 and `Constructs` 10.4.2 → 10.8.1 to clear NU1901 vulnerability advisory.
- Added `#pragma warning disable CS0618, CS0612` around deprecated `CloudFrontWebDistribution` usage (the CDK library's own deprecation, not related to the .NET migration).

---

## Next Steps (Non-blocking)

- **Bundling/Minification**: `System.Web.Optimization` (`BundleConfig.cs`) was removed. Static JS/CSS files are currently served via CDN links in `_Layout.cshtml`. A build-time bundler (e.g., WebOptimizer.Core, Node-based webpack/esbuild) should be configured for production asset optimization.
- **Database Migrations**: The application uses `EnsureCreated()` with `HasData()` seeding. For production, EF Core migrations (`dotnet ef migrations add InitialCreate; dotnet ef database update`) should be set up.
- **Cognito Authentication – HTTPS Requirement**: The README notes that Cognito's Hosted UI requires HTTPS login redirects (except `http://localhost`). Ensure the production environment uses HTTPS. The Kestrel/reverse-proxy HTTPS configuration is not included here.
- **AppRunner CDK Stack**: The `EcsStack.cs` deploys to ECS; the `Services/Authentication` is forced to `local` for ECS per README. Verify after deployment.
- **CloudFrontWebDistribution Deprecation**: `CoreStack.cs` uses the deprecated `CloudFrontWebDistribution` CDK construct. Consider migrating to the new `Distribution` construct in a separate CDK-focused task.
- **Bookstore.Data targeting `net10.0` only**: This was a deliberate choice due to EF Core's lack of `netstandard2.0` support. If future consumers on .NET Framework need to reference `Bookstore.Data`, a separate interface/contract package (multi-targeted) would be required.
