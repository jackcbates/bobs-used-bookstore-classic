# Migration Summary: .NET Framework 4.8 → .NET 10 (BobsBookstoreClassic)

## Final Build Result
`dotnet build BobsBookstoreClassic.sln` — **Build succeeded. 0 Warning(s). 0 Error(s).**

All 5 projects target `net10.0` and compile cleanly:
- Bookstore.Common
- Bookstore.Domain
- Bookstore.Data
- Bookstore.Web
- Bookstore.Cdk

---

## Work Done This Cycle

The AWS Transform Custom CLI (atx) had already applied the core .NET Framework → net10.0 transformation before this cycle began. This cycle validated the atx output, resolved all remaining warnings, and drove the build to a clean zero-warning state.

### Package Upgrades (Bookstore.Data.csproj)
| Package | Before | After | Reason |
|---|---|---|---|
| `Magick.NET-Q8-AnyCPU` | 14.6.0 | **14.17.2** | Eliminated 650 NU1901/NU1902/NU1903 vulnerability warnings |
| `Microsoft.EntityFrameworkCore` | 9.0.7 | **10.0.12** | Aligned to net10.0 target framework stable release |
| `Microsoft.EntityFrameworkCore.SqlServer` | 9.0.7 | **10.0.12** | Aligned to net10.0 stable |
| `Microsoft.Extensions.Configuration.Abstractions` | 9.0.7 | **10.0.12** | Aligned to net10.0 stable |

### Package Upgrades (Bookstore.Web.csproj)
| Package | Before | After | Reason |
|---|---|---|---|
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | 10.0.0-preview.5.25277.114 | **10.0.12** | Replaced preview with stable GA release |
| `Microsoft.EntityFrameworkCore.SqlServer` | 10.0.0-preview.5.25277.114 | **10.0.12** | Replaced preview with stable GA release |

### Package Upgrades (Bookstore.Cdk.csproj)
| Package | Before | After | Reason |
|---|---|---|---|
| `Amazon.CDK.Lib` | 2.188.0 | **2.272.0** | Fixed NU1901 low severity vulnerability (GHSA-464c-974j-9xm6) |
| `Constructs` | 10.4.2 | **10.8.1** | Aligned with CDK.Lib upgrade |

### CDK Code Migration (CoreStack.cs)
Migrated `CreateCloudFrontDistribution()` from the deprecated `CloudFrontWebDistribution` (CS0618/CS0612) to the modern `Distribution` + `S3BucketOrigin.WithOriginAccessControl()` API:

- Removed: `CloudFrontWebDistribution`, `CloudFrontWebDistributionProps`, `SourceConfiguration`, `S3OriginConfig`, `Behavior`, `CloudFrontAllowedMethods`, `OriginAccessIdentity`, manual `PolicyStatement` bucket policy
- Added: `using Amazon.CDK.AWS.CloudFront.Origins;`, `Distribution`, `DistributionProps`, `BehaviorOptions`, `AllowedMethods`, `S3BucketOrigin.WithOriginAccessControl()` (OAC instead of OAI)

This brings the CDK code to the recommended modern CloudFront pattern using Origin Access Control.

---

## Architecture After Migration

The application is a standard ASP.NET Core MVC app on .NET 10:
- **Program.cs** — minimal hosting model with DI, authentication, NLog, and middleware pipeline
- **EF Core 10.0.12** (Code-First, SQL Server) — data access via `ApplicationDbContext`
- **Amazon Cognito** (optional) — OpenID Connect authentication
- **AWS SSM** — external configuration (connection strings, auth settings)
- **Amazon S3** — image file storage (optional)
- **Amazon Rekognition** — image validation (optional)
- **NLog 5 / AWS.Logger.NLog** — structured logging
- **AWS CDK .NET** — infrastructure as code (ECS, RDS, Cognito, S3, CloudFront)

---

## Next Steps

None blocking. The build is clean. Items for future consideration:

- **NLog upgrade**: Current versions (NLog 5.4.0, NLog.Web.AspNetCore 5.3.15, AWS.Logger.NLog 3.3.4) work correctly. Latest versions are NLog 6.2.1 / NLog.Web.AspNetCore 6.2.1 / AWS.Logger.NLog 5.0.1. These are major version bumps with potential API changes (e.g., NLog 6 lambda configuration DSL). Evaluate and upgrade in a dedicated cycle.
- **AWS SDK upgrade**: Current AWSSDK.* packages are on v3.7.x. AWS SDK v4 is now available (e.g., AWSSDK.S3 4.0.x). This is a major version change; evaluate breaking changes in the v4 migration guide before upgrading.
- **CdkNag upgrade**: Current Cdklabs.CdkNag 2.35.66; latest is 3.0.2. Major version bump — review CdkNag 3.x migration guide before upgrading.
- **Bookstore.Common targets netstandard2.0**: Acceptable for a shared constants library but could be moved to net10.0 for consistency. No functional impact.
