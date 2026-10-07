# Assessment Report: BobsBookstoreClassic

## Solution Overview

| Attribute | Value |
|-----------|-------|
| **Solution Name** | BobsBookstoreClassic |
| **Total Projects** | 5 |
| **Target Framework** | net10.0 |
| **Total Lines of Code** | 8592 |
| **Overall Complexity** | Critical |
| **Total NuGet Packages** | 62 (across all projects) |
| **Incompatible Packages** | 12 |
| **.NET Core Readiness** | Partial |
| **Linux Readiness** | Partial |

## Executive Summary

**Solution Migration Mode: COMPLEX**

BobsBookstoreClassic is a layered ASP.NET MVC 5 web application for an online used bookstore, deployed via AWS CDK on ECS. The architecture follows a classic four-tier pattern: **Bookstore.Web** (MVC 5 presentation on .NET Framework 4.8) → **Bookstore.Data** (EF6 data access on .NET Framework 4.8) → **Bookstore.Domain** (domain models on .NET Framework 4.8) → **Bookstore.Common** (shared constants on netstandard2.0). A separate **Bookstore.Cdk** project (net6.0) handles infrastructure-as-code deployment.

The solution uses OWIN middleware for OpenID Connect authentication, Autofac for dependency injection (via Autofac.Mvc5), NLog with AWS CloudWatch integration for logging, Entity Framework 6 for data access, and AWS SDK services (S3, Rekognition, SSM). The web project has an Admin area with its own controllers and views.

- **1 Low-complexity project** (Bookstore.Domain) — pure domain model library with no NuGet packages; only needs project format conversion
- **2 Medium-complexity projects** (Bookstore.Data, Bookstore.Cdk) — Data needs format conversion and EF6→EF Core migration; Cdk needs a TFM bump from net6.0 to net10.0
- **1 Critical-complexity project** (Bookstore.Web) — 54 packages (12 incompatible), MVC 5→ASP.NET Core MVC, OWIN→Core middleware, Autofac.Mvc5→Core DI, auth migration, old-style→SDK-style
- **1 project requires no migration** (Bookstore.Common) — already targets netstandard2.0

The primary transformation challenge is the Bookstore.Web project: it requires a complete rewrite of its hosting model (Global.asax + OWIN Startup → Program.cs), authentication pipeline (OWIN OpenID Connect → ASP.NET Core Authentication), dependency injection (Autofac.Mvc5 → Autofac.Extensions.DependencyInjection or built-in DI), bundling/optimization (System.Web.Optimization → static file serving), and all 42 Razor views must be updated from MVC 5 helpers to ASP.NET Core Tag Helpers.

### Key Statistics

| Metric | Count |
|--------|-------|
| Projects requiring format conversion | 3 (Bookstore.Domain, Bookstore.Data, Bookstore.Web — legacy to SDK-style) |
| Blocking issues | 0 |
| Razor views to migrate | 42 |
| Controllers to migrate | 15 |
| Total estimated changes | 114 |

## Project Analysis Table

| Project | Current Framework | Target | LOC | Packages | Incompatible | Complexity |
|---------|-------------------|--------|-----|----------|--------------|------------|
| Bookstore.Common | netstandard2.0 | netstandard2.0 | 6 | 0 | 0 | Low |
| Bookstore.Domain | net4.8 | net10.0 | 1813 | 0 | 0 | Low |
| Bookstore.Data | net4.8 | net10.0 | 1042 | 4 | 0 | Medium |
| Bookstore.Cdk | net6.0 | net10.0 | 595 | 4 | 0 | Medium |
| Bookstore.Web | net4.8 | net10.0 | 5136 | 54 | 12 | Critical |

## Cross-Project Package Summary

| Package | Used By | Version(s) | Compatible | Notes |
|---------|---------|------------|------------|-------|
| EntityFramework | Bookstore.Data, Bookstore.Web | 6.5.1 | Yes | Latest 6.5.2; migrate to Microsoft.EntityFrameworkCore for full .NET 10 support |
| AWSSDK.Rekognition | Bookstore.Data, Bookstore.Web | 3.7.400.129 | Yes | Latest 4.0.101.1; major version upgrade available |
| AWSSDK.S3 | Bookstore.Data, Bookstore.Web | 3.7.416.5 | Yes | Latest 4.0.104.1; major version upgrade available |
| AWSSDK.Core | Bookstore.Web | 3.7.402.35 | Yes | Latest 4.0.102.8; transitive dependency of other AWS SDK packages |
| AWSSDK.CloudWatchLogs | Bookstore.Web | 3.7.410.17 | Yes | Latest 4.0.105; upgrade alongside other AWS SDK packages |
| AWSSDK.SimpleSystemsManagement | Bookstore.Web | 3.7.404.10 | Yes | Latest 4.0.104.1; major version upgrade available |
| Autofac | Bookstore.Web | 8.2.1 | Yes | Core library is compatible; Autofac.Mvc5/Owin integrations are not |
| Autofac.Mvc5 | Bookstore.Web | 6.1.0 | No | .NETFramework4.7.2 only; replace with Autofac.Extensions.DependencyInjection |
| Autofac.Owin | Bookstore.Web | 7.1.0 | No | .NETFramework4.7.2 only; remove — OWIN replaced by ASP.NET Core middleware |
| Microsoft.AspNet.Mvc | Bookstore.Web | 5.3.0 | No | .NETFramework only; replace with built-in ASP.NET Core MVC |
| Microsoft.Owin | Bookstore.Web | 4.2.2 | No | .NETFramework4.5 only; replace with ASP.NET Core middleware pipeline |
| Microsoft.Owin.Security.OpenIdConnect | Bookstore.Web | 4.2.2 | No | Replace with Microsoft.AspNetCore.Authentication.OpenIdConnect |
| NLog | Bookstore.Web | 5.4.0 | Yes | Latest 6.2.1; upgrade and add NLog.Web.AspNetCore integration |
| Newtonsoft.Json | Bookstore.Web | 13.0.3 | Yes | Latest 13.0.4; minor upgrade available |
| Magick.NET-Q8-AnyCPU | Bookstore.Data | 14.6.0 | Yes | Latest 14.17.2; targets netstandard2.0 and net8.0 |

## Cross-Project Dependencies

Bookstore.Web (Critical)
  - Bookstore.Common (Low — no migration needed)
  - Bookstore.Data (Medium)
    - Bookstore.Domain (Low)
  - Bookstore.Domain (Low)

Bookstore.Cdk (Medium)
  - Bookstore.Common (Low — no migration needed)

### Recommended Transformation Order (Dependency-First)

1. **Bookstore.Domain** — leaf library with zero NuGet dependencies; convert to SDK-style targeting net10.0
2. **Bookstore.Data** — depends on Bookstore.Domain; convert to SDK-style, migrate EF6 → EF Core, upgrade AWS SDK packages
3. **Bookstore.Cdk** — depends only on Bookstore.Common (no migration needed); update TFM from net6.0 to net10.0 and upgrade CDK packages
4. **Bookstore.Web** — depends on all other projects; full MVC 5 → ASP.NET Core MVC migration (migrate last)

*Note: Bookstore.Common (netstandard2.0) requires no migration and is excluded from the transformation order.*

## Key Findings

1. **MVC 5 to ASP.NET Core MVC rewrite required**: Bookstore.Web is a full ASP.NET MVC 5 application with 15 controllers (including an Admin area with 6 controllers), 42 Razor views, OWIN middleware, and Autofac DI — all of which must be migrated to their ASP.NET Core equivalents.
2. **OWIN authentication pipeline must be replaced**: The application uses Microsoft.Owin.Security.OpenIdConnect and Microsoft.Owin.Security.Cookies for authentication via an OWIN Startup class. This must be converted to ASP.NET Core's built-in authentication middleware with Microsoft.AspNetCore.Authentication.OpenIdConnect.
3. **Entity Framework 6 to EF Core migration**: Both Bookstore.Data and Bookstore.Web reference EntityFramework 6.5.1. While EF6 is technically compatible (netstandard2.1), migrating to EF Core is recommended for full .NET 10 tooling support and performance benefits.
4. **Three projects require legacy-to-SDK format conversion**: Bookstore.Domain, Bookstore.Data, and Bookstore.Web use old-style .csproj format with `packages.config` (Web) and explicit `<Compile>` items. All three need conversion to SDK-style project format.
5. **AWS SDK major version upgrade available**: All AWS SDK packages (S3, Rekognition, CloudWatch, SSM) are on v3.7.x while v4.x is available. The v3→v4 upgrade includes breaking API changes that must be addressed.
6. **Autofac DI container integration needs replacement**: Autofac.Mvc5 and Autofac.Owin are .NET Framework-only. Replace with Autofac.Extensions.DependencyInjection for ASP.NET Core, or migrate entirely to the built-in Microsoft.Extensions.DependencyInjection container.
7. **CDK project needs TFM update**: Bookstore.Cdk targets net6.0 (end of support) and should be updated to net10.0 along with CDK library upgrades.
8. **Bookstore.Common needs no migration**: Already targets netstandard2.0, which is compatible with all .NET platforms — leave unchanged.

## External Dependencies

| Dependency | Type | Impact |
|------------|------|--------|
| AWS S3 | Cloud Service | Used for file storage (cover images); AWSSDK.S3 package upgrade required |
| AWS Rekognition | Cloud Service | Used for image validation; AWSSDK.Rekognition package upgrade required |
| AWS CloudWatch Logs | Cloud Service | Logging target via NLog; AWS.Logger.NLog package upgrade required |
| AWS Systems Manager (SSM) | Cloud Service | Configuration/secrets management; AWSSDK.SimpleSystemsManagement package upgrade required |
| SQL Server (via EF6) | Database | Data access via Entity Framework 6; requires EF Core migration and connection string update |
| OpenID Connect Provider | Identity | OWIN-based OpenID Connect auth; must migrate to ASP.NET Core Authentication middleware |
| AWS CDK / ECS | Cloud Service | Infrastructure deployment; CDK project needs net10.0 TFM and package updates |

## Actionable Next Steps

1. **Phase 1 — Leaf libraries** (Low risk): Convert Bookstore.Domain to SDK-style net10.0. Verify it builds and all downstream projects still compile.
2. **Phase 2 — Data layer** (Medium risk): Convert Bookstore.Data to SDK-style net10.0. Migrate Entity Framework 6 to EF Core (update DbContext, configurations, initializer). Upgrade AWS SDK and Magick.NET packages.
3. **Phase 3 — CDK project** (Low risk): Update Bookstore.Cdk TFM from net6.0 to net10.0. Upgrade Amazon.CDK.Lib, Constructs, and Cdklabs.CdkNag to latest versions.
4. **Phase 4 — Web application** (High risk): Full migration of Bookstore.Web — convert to SDK-style with `Microsoft.NET.Sdk.Web`, replace Global.asax/OWIN Startup with Program.cs, migrate all 54 packages, rewrite authentication from OWIN to ASP.NET Core middleware, replace Autofac.Mvc5 with Autofac.Extensions.DependencyInjection, convert BundleConfig to static files, update all 42 Razor views to use Tag Helpers, and migrate Web.config settings to appsettings.json.
5. **Phase 5 — Integration validation** (Medium risk): End-to-end testing of authentication flow, S3 file operations, Rekognition image validation, database operations, and Admin area functionality. Update CDK stack if Dockerfile or deployment configuration changed.

---

## Per-Project Assessment Details

### Bookstore.Domain

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 1813 |
| **NuGet Packages** | 0 |
| **Project References** | 0 |
| **Complexity** | Low |
| **Estimated Changes** | 1 |

#### Migration Analysis

##### Migration Strategy

1. Convert the old-style `.csproj` (with `<Import>`, explicit `<Compile>` items, `<PropertyGroup>` for Debug/Release) to SDK-style `<Project Sdk="Microsoft.NET.Sdk">` targeting `net10.0`.
2. Remove `Properties/AssemblyInfo.cs` and enable `<GenerateAssemblyInfo>true</GenerateAssemblyInfo>` (SDK default). Preserve `RootNamespace` (`Bookstore.Domain`) and `AssemblyName` (`Bookstore.Domain`).
3. Remove all explicit `<Compile Include="..."/>` entries — SDK-style projects auto-include `*.cs` files via default globs.
4. Remove `<Reference>` entries for BCL assemblies (`System`, `System.Core`, `System.ComponentModel.DataAnnotations`, etc.) — these are implicitly referenced on net10.0. The `System.ComponentModel.DataAnnotations` namespace is available in the `System.ComponentModel.Annotations` NuGet package if needed, but it is part of the net10.0 shared framework.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| DataAnnotations API differences | Low | `System.ComponentModel.DataAnnotations` is available on net10.0 but verify all attributes used (e.g., `[Required]`, `[StringLength]`) are present in the shared framework |
| Namespace collisions after glob inclusion | Low | SDK-style auto-includes all .cs files; verify no unexpected files in the project directory |

##### Recommendations

1. Migrate this project first as it is a leaf dependency with zero NuGet packages — the simplest and safest starting point.
2. After conversion, build and verify that Bookstore.Data and Bookstore.Web still compile with the updated project reference.

##### Cross-Project Impact

Bookstore.Domain is referenced by Bookstore.Data and Bookstore.Web. It must be migrated before both. Since it has no NuGet dependencies, its migration risk is minimal and will not cascade package conflicts to dependents.

---

### Bookstore.Data

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 1042 |
| **NuGet Packages** | 4 |
| **Project References** | 1 |
| **Complexity** | Medium |
| **Estimated Changes** | 6 |

#### Package Compatibility (4 packages, 0 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| AWSSDK.Rekognition | 3.7.400.129 | COMPATIBLE | UpgradePackage |
| AWSSDK.S3 | 3.7.416.5 | COMPATIBLE | UpgradePackage |
| EntityFramework | 6.5.1 | COMPATIBLE | UpgradePackage |
| Magick.NET-Q8-AnyCPU | 14.6.0 | COMPATIBLE | UpgradePackage |

#### Project Dependencies (1)

- Bookstore.Domain

#### Legacy Files Inventory (1 files across 1 kinds)

| Kind | Files |
|------|------:|
| `.config` | 1 |

#### Migration Analysis

##### Migration Strategy

1. Convert the old-style `.csproj` to SDK-style `<Project Sdk="Microsoft.NET.Sdk">` targeting `net10.0`. Remove explicit `<Compile>` items, `<Reference>` entries for BCL assemblies, and build configuration `<PropertyGroup>` blocks.
2. Remove `Properties/AssemblyInfo.cs`; preserve `RootNamespace` (`Bookstore.Data`) and `AssemblyName` (`Bookstore.Data`).
3. Migrate Entity Framework 6 to Entity Framework Core: replace `EntityFramework` 6.5.1 with `Microsoft.EntityFrameworkCore.SqlServer` (latest for net10.0). Update `ApplicationDbContext` to inherit from `Microsoft.EntityFrameworkCore.DbContext`, replace `EntityTypeConfiguration<T>` with `IEntityTypeConfiguration<T>`, update `BookstoreDbInitializer` from `CreateDatabaseIfNotExists<T>` to EF Core migrations or `EnsureCreated`, and replace `System.Data.Entity` usings with `Microsoft.EntityFrameworkCore`.
4. Upgrade `AWSSDK.S3` to latest 4.x and `AWSSDK.Rekognition` to latest 4.x. Address breaking API changes in v3→v4 (namespace and client constructor changes).
5. Upgrade `Magick.NET-Q8-AnyCPU` from 14.6.0 to latest 14.17.2.
6. Convert `App.config` connection string to be consumed via `IConfiguration` / dependency injection rather than `ConfigurationManager`.
7. Replace `System.Configuration.ConfigurationManager` usage in `BookstoreConfiguration.cs` with injected `IConfiguration`.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| EF6 → EF Core behavioral differences | Medium | Lazy loading off by default, Include/ThenInclude syntax changes, seed data approach changes |
| AWS SDK v3 → v4 breaking changes | Medium | Client constructors, credential chain, and some API shapes changed in v4 |
| ConfigurationManager removal | Low | Must inject IConfiguration; BookstoreConfiguration.cs and connection string handling affected |

##### Recommendations

1. Migrate after Bookstore.Domain since this project depends on it.
2. Write integration tests for repository classes before migration to catch EF behavioral differences.
3. Consider keeping EF6 initially (it is compatible on netstandard2.1) and migrating to EF Core in a separate pass if risk needs to be reduced.

##### Cross-Project Impact

Bookstore.Data is referenced by Bookstore.Web. The EF6 → EF Core migration here directly impacts Bookstore.Web's `DependencyInjectionSetup.cs` (DI registration of DbContext) and any direct EF usage in the web layer. The DbContext registration pattern changes from `new ApplicationDbContext()` to `services.AddDbContext<ApplicationDbContext>()`.

---

### Bookstore.Cdk

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net6.0 |
| **Lines of Code** | 595 |
| **NuGet Packages** | 4 |
| **Project References** | 1 |
| **Complexity** | Medium |
| **Estimated Changes** | 4 |

#### Package Compatibility (4 packages, 0 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| Amazon.CDK.Lib | 2.188.0 | COMPATIBLE | UpgradePackage |
| Cdklabs.CdkNag | 2.35.66 | COMPATIBLE | UpgradePackage |
| Constructs | 10.4.2 | COMPATIBLE | UpgradePackage |
| Amazon.Jsii.Analyzers | * | COMPATIBLE | KeepPackage |

#### Project Dependencies (1)

- Bookstore.Common

#### Migration Analysis

##### Migration Strategy

1. Update `<TargetFramework>` from `net6.0` to `net10.0` in the `.csproj`. The project is already SDK-style, so no format conversion is needed.
2. Upgrade `Amazon.CDK.Lib` from 2.188.0 to latest (2.272.0). Review CDK construct API changes between these versions.
3. Upgrade `Cdklabs.CdkNag` from 2.35.66 to latest (3.0.2). Note the major version bump — check for breaking API changes in CdkNag 3.x.
4. Upgrade `Constructs` from 10.4.2 to latest (10.8.1).
5. Remove `<RollForward>Major</RollForward>` from the `.csproj` — it was needed to run net6.0 on a newer runtime, but is unnecessary when targeting net10.0 directly.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| Cdklabs.CdkNag 2.x → 3.x breaking changes | Medium | Major version bump; rule suppressions and API may have changed |
| CDK construct API changes | Low | CDK Lib 2.188→2.272 may deprecate or rename some constructs |
| Dockerfile update may be needed | Low | If the ECS stack references a .NET 4.8 base image, it must be updated to a net10.0 image |

##### Recommendations

1. Migrate this project after Bookstore.Domain (its only code dependency, Bookstore.Common, needs no migration).
2. Run `cdk diff` after upgrading to verify infrastructure changes are expected.
3. Update the Dockerfile referenced by the ECS stack to use a .NET 10 base image.

##### Cross-Project Impact

Bookstore.Cdk depends on Bookstore.Common (no migration needed) and has no downstream dependents. Its migration is independent of the web/data layer migration and can be done in parallel with Bookstore.Data.

---

### Bookstore.Web

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | net4.8 |
| **Lines of Code** | 5136 |
| **NuGet Packages** | 54 |
| **Project References** | 3 |
| **Complexity** | Critical |
| **Estimated Changes** | 103 |

#### Package Compatibility (54 packages, 12 incompatible)

| Package | Version | Compatibility | Recommendation |
|---------|---------|---------------|----------------|
| Antlr | 3.5.0.2 | COMPATIBLE | ReplacePackage |
| Autofac | 8.2.1 | COMPATIBLE | KeepPackage |
| Autofac.Mvc5 | 6.1.0 | INCOMPATIBLE | ReplacePackage |
| Autofac.Owin | 7.1.0 | INCOMPATIBLE | ReplacePackage |
| AWS.Logger.Core | 3.3.3 | COMPATIBLE | UpgradePackage |
| AWS.Logger.NLog | 3.3.4 | COMPATIBLE | UpgradePackage |
| AWSSDK.CloudWatchLogs | 3.7.410.17 | COMPATIBLE | UpgradePackage |
| AWSSDK.Core | 3.7.402.35 | COMPATIBLE | UpgradePackage |
| AWSSDK.Rekognition | 3.7.400.129 | COMPATIBLE | UpgradePackage |
| AWSSDK.S3 | 3.7.416.5 | COMPATIBLE | UpgradePackage |
| AWSSDK.SimpleSystemsManagement | 3.7.404.10 | COMPATIBLE | UpgradePackage |
| EntityFramework | 6.5.1 | COMPATIBLE | UpgradePackage |
| jQuery | 3.7.1 | COMPATIBLE | ReplacePackage |
| jQuery.Validation | 1.21.0 | COMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Mvc | 5.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Razor | 3.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.Web.Optimization | 1.1.3 | INCOMPATIBLE | ReplacePackage |
| Microsoft.AspNet.WebPages | 3.3.0 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Bcl.AsyncInterfaces | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Bcl.Memory | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Bcl.TimeProvider | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.CodeDom.Providers.DotNetCompilerPlatform | 4.1.0 | COMPATIBLE | ReplacePackage |
| Microsoft.Extensions.DependencyInjection.Abstractions | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.Extensions.Logging.Abstractions | 9.0.3 | COMPATIBLE | ReplacePackage |
| Microsoft.IdentityModel.Abstractions | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.JsonWebTokens | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Logging | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Protocols | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Protocols.OpenIdConnect | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.IdentityModel.Tokens | 8.7.0 | COMPATIBLE | UpgradePackage |
| Microsoft.jQuery.Unobtrusive.Validation | 4.0.0 | COMPATIBLE | ReplacePackage |
| Microsoft.Owin | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Host.SystemWeb | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security.Cookies | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Owin.Security.OpenIdConnect | 4.2.2 | INCOMPATIBLE | ReplacePackage |
| Microsoft.Web.Infrastructure | 2.0.1 | COMPATIBLE | ReplacePackage |
| Modernizr | 2.8.3 | COMPATIBLE | ReplacePackage |
| Newtonsoft.Json | 13.0.3 | COMPATIBLE | UpgradePackage |
| NLog | 5.4.0 | COMPATIBLE | UpgradePackage |
| Owin | 1.0 | INCOMPATIBLE | ReplacePackage |
| System.Buffers | 4.6.1 | COMPATIBLE | ReplacePackage |
| System.Diagnostics.DiagnosticSource | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.IdentityModel.Tokens.Jwt | 8.7.0 | COMPATIBLE | UpgradePackage |
| System.IO.Pipelines | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Memory | 4.6.3 | COMPATIBLE | ReplacePackage |
| System.Numerics.Vectors | 4.6.1 | COMPATIBLE | ReplacePackage |
| System.Runtime.CompilerServices.Unsafe | 6.1.2 | COMPATIBLE | ReplacePackage |
| System.Text.Encoding | 4.3.0 | COMPATIBLE | ReplacePackage |
| System.Text.Encodings.Web | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Text.Json | 9.0.3 | COMPATIBLE | ReplacePackage |
| System.Threading.Tasks.Extensions | 4.6.3 | COMPATIBLE | ReplacePackage |
| System.ValueTuple | 4.6.1 | COMPATIBLE | ReplacePackage |
| WebGrease | 1.6.0 | COMPATIBLE | ReplacePackage |

**Package Notes:**
- **Autofac.Mvc5** → Replace with `Autofac.Extensions.DependencyInjection` for ASP.NET Core integration. Latest 7.0.0 still targets .NETFramework4.8.1 only.
- **Autofac.Owin** → Remove; OWIN pipeline replaced by ASP.NET Core middleware. Latest 8.0.0 still targets .NETFramework4.8.1 only.
- **Microsoft.AspNet.Mvc / Razor / WebPages** → Remove; replaced by built-in ASP.NET Core MVC (Microsoft.NET.Sdk.Web).
- **Microsoft.AspNet.Web.Optimization** → Remove; replace bundling with static file serving, LibMan, or bundleconfig.json.
- **Microsoft.Owin.* / Owin** → Remove all 6 OWIN packages; replace with ASP.NET Core middleware. Use `Microsoft.AspNetCore.Authentication.OpenIdConnect` and `Microsoft.AspNetCore.Authentication.Cookies`.
- **jQuery / jQuery.Validation / Microsoft.jQuery.Unobtrusive.Validation / Modernizr** → Client-side assets; remove NuGet references and deliver via wwwroot/LibMan/CDN.
- **Antlr / WebGrease / Microsoft.CodeDom.Providers.DotNetCompilerPlatform / Microsoft.Web.Infrastructure** → Build-time/tooling packages; remove — not needed with SDK-style projects.
- **Microsoft.Bcl.AsyncInterfaces / Memory / TimeProvider, System.Buffers / Memory / Numerics.Vectors / Runtime.CompilerServices.Unsafe / Text.Encoding / Encodings.Web / Text.Json / IO.Pipelines / Threading.Tasks.Extensions / ValueTuple / DiagnosticSource** → Polyfill/inbox packages; remove PackageReferences — these are part of the net10.0 shared framework.
- **Microsoft.Extensions.DependencyInjection.Abstractions / Logging.Abstractions** → Remove explicit references; inbox in the ASP.NET Core shared framework.

#### Project Dependencies (3)

- Bookstore.Common
- Bookstore.Data
- Bookstore.Domain

#### Legacy Files Inventory (49 files across 3 kinds)

| Kind | Files |
|------|------:|
| `.asax` | 1 |
| `.cshtml` | 42 |
| `.config` | 6 |

#### Migration Analysis

##### Migration Strategy

1. Convert the old-style `.csproj` to SDK-style using `<Project Sdk="Microsoft.NET.Sdk.Web">` targeting `net10.0`. Remove all explicit `<Compile>`, `<Content>`, `<Reference>`, and `<None>` items. Set `OutputType` to `Exe`. Preserve `RootNamespace` (`Bookstore.Web`) and `AssemblyName` (`Bookstore.Web`).
2. Delete `packages.config` and convert all package references to `<PackageReference>` elements in the `.csproj`. Remove the 12 incompatible packages, the 14 inbox polyfill packages, the 8 content/tooling packages, and retain/upgrade the remaining packages.
3. Replace `Global.asax` and `Global.asax.cs` with a `Program.cs` using the ASP.NET Core minimal hosting model. Compose service registration (DI, authentication, logging, routing) and middleware pipeline order from the existing `App_Start` classes: `DependencyInjectionSetup.cs`, `AuthenticationSetup.cs`, `LoggingSetup.cs`, `ConfigurationSetup.cs`, `FilterConfig.cs`, `RouteConfig.cs`, and `BundleConfig.cs`.
4. Replace the OWIN `Startup.cs` (`Startup.Configuration(IAppBuilder)`) with ASP.NET Core middleware: convert `app.UseCookieAuthentication()` → `builder.Services.AddAuthentication().AddCookie()` and `app.UseOpenIdConnectAuthentication()` → `builder.Services.AddAuthentication().AddOpenIdConnect()`. Migrate the `LocalAuthenticationMiddleware` OWIN middleware to ASP.NET Core `IMiddleware`.
5. Replace `Autofac.Mvc5` DI registration (`DependencyInjectionSetup.cs`) with `Autofac.Extensions.DependencyInjection`: use `builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory())` and `builder.Host.ConfigureContainer<ContainerBuilder>(...)`.
6. Migrate `Web.config` settings (`<appSettings>`, `<connectionStrings>`, custom sections) to `appsettings.json`. Replace `ConfigurationManager.AppSettings["key"]` calls with injected `IConfiguration["key"]` or strongly-typed `IOptions<T>`.
7. Replace `BundleConfig.cs` and `@Scripts.Render`/`@Styles.Render` in views with direct `<script>`/`<link>` tags referencing files from `wwwroot`. Move static assets from `Content/` and `Scripts/` to `wwwroot/`.
8. Update `RouteConfig.cs` conventional routing to ASP.NET Core endpoint routing in `Program.cs`: `app.MapControllerRoute(...)` with area support for the Admin area.
9. Convert the `AdminAreaRegistration.cs` to ASP.NET Core area routing: add `[Area("Admin")]` attribute to Admin controllers and register area routes in `Program.cs`.
10. Update all 42 Razor views: replace `@Html.ActionLink` → `<a asp-action>`, `@Html.BeginForm` → `<form asp-action>`, `@Html.TextBoxFor` / `@Html.DropDownListFor` → `<input asp-for>` / `<select asp-for>`, `@Html.ValidationMessageFor` → `<span asp-validation-for>`. Add `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` to `_ViewImports.cshtml`.
11. Update all 15 controllers: replace `using System.Web.Mvc` → `using Microsoft.AspNetCore.Mvc`, change `HttpPostedFileBase` → `IFormFile`, `Request.Files` → `Request.Form.Files`, `Server.MapPath` → `IWebHostEnvironment.ContentRootPath`, `ConfigurationManager` → injected `IConfiguration`.
12. Replace `Properties/AssemblyInfo.cs` with SDK-generated assembly info.
13. Migrate NLog configuration: upgrade to NLog 6.x, add `NLog.Web.AspNetCore` package, configure via `builder.Logging.AddNLog()` in Program.cs. Upgrade `AWS.Logger.NLog` to 5.x for CloudWatch integration.
14. Remove `Views/Web.config` and `Areas/Admin/Views/web.config` — Razor view configuration is handled by `_ViewImports.cshtml` in ASP.NET Core.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| OWIN → ASP.NET Core auth migration | High | OpenID Connect flow, cookie settings, claims transformation, and the custom LocalAuthenticationMiddleware must be carefully ported |
| MVC 5 → ASP.NET Core MVC view syntax | Medium | 42 Razor views need Tag Helper conversion; @Html helper patterns differ |
| Autofac.Mvc5 → Autofac.Extensions.DependencyInjection | Medium | DI registration patterns change; DependencyResolver.Current must be removed |
| BundleConfig / Web.Optimization removal | Medium | All bundle references in views must be replaced with direct asset paths |
| Admin area routing changes | Medium | AreaRegistration class pattern replaced by [Area] attribute + endpoint routing |
| AWS SDK v3 → v4 API changes | Medium | Client constructors, async patterns, and some response types changed |
| Static file path changes | Low | Content/ and Scripts/ directories move to wwwroot/; all references must be updated |
| HttpContext.Current usage | Medium | Must be replaced with IHttpContextAccessor injection wherever used (HttpContextExtensions.cs, controllers) |

##### Recommendations

1. Migrate this project last — it depends on all three other projects being on net10.0 first.
2. Start with the hosting/startup migration (Program.cs) before tackling individual controllers and views.
3. Migrate authentication in isolation and validate the OpenID Connect flow end-to-end before proceeding to controller migration.
4. Use bulk sed/regex for the mechanical Razor view Tag Helper conversions (namespace replacements, `@Html.ActionLink` → `<a asp-action>`, etc.), then manually review non-trivial views.
5. Consider keeping Autofac (via Autofac.Extensions.DependencyInjection) rather than rewriting all registrations to built-in DI, to minimize churn.
6. Add NLog.Web.AspNetCore for ASP.NET Core integration and update the NLog config to use ASP.NET Core layout renderers.

##### Cross-Project Impact

Bookstore.Web depends on Bookstore.Common (no migration needed), Bookstore.Domain, and Bookstore.Data. It must be migrated after all three. The EF Core migration in Bookstore.Data will change how the DbContext is registered (from `new ApplicationDbContext()` to `services.AddDbContext<ApplicationDbContext>()`), requiring corresponding changes in Bookstore.Web's DI setup. The Bookstore.Cdk project may need Dockerfile updates to use a .NET 10 base image once Bookstore.Web targets net10.0.

---

### Bookstore.Common

#### Project Metrics

| Metric | Value |
|--------|-------|
| **Framework** | netstandard2.0 |
| **Lines of Code** | 6 |
| **NuGet Packages** | 0 |
| **Project References** | 0 |
| **Complexity** | Low |
| **Estimated Changes** | 0 |

#### Migration Analysis

##### Migration Strategy

1. **No migration needed.** Bookstore.Common targets `netstandard2.0`, which is fully compatible with net10.0 and all other target frameworks in the solution. Leave this project completely unchanged.

##### Risks & Architectural Concerns

| Risk | Severity | Notes |
|------|----------|-------|
| None | Low | netstandard2.0 is cross-platform compatible; no action required |

##### Recommendations

1. Do not modify this project. It serves as a shared constants library consumed by both Bookstore.Cdk (net6.0→net10.0) and Bookstore.Web (net4.8→net10.0) and its netstandard2.0 target ensures compatibility with both.

##### Cross-Project Impact

Bookstore.Common is referenced by Bookstore.Cdk and Bookstore.Web. Because it targets netstandard2.0 and requires no changes, it imposes no migration ordering constraint on its dependents.
