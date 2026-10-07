#nullable enable
using Amazon.Rekognition;
using Amazon.S3;
using Amazon.SimpleSystemsManagement;
using Amazon.SimpleSystemsManagement.Model;
using BobsBookstoreClassic.Data;
using Bookstore.Common;
using Bookstore.Data;
using Bookstore.Data.FileServices;
using Bookstore.Data.ImageResizeService;
using Bookstore.Data.ImageValidationServices;
using Bookstore.Data.Repositories;
using Bookstore.Domain;
using Bookstore.Domain.Addresses;
using Bookstore.Domain.Books;
using Bookstore.Domain.Carts;
using Bookstore.Domain.Customers;
using Bookstore.Domain.Offers;
using Bookstore.Domain.Orders;
using Bookstore.Domain.ReferenceData;
using Bookstore.Web.Helpers;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Targets;
using NLog.Web;
using System.Security.Claims;
using LogLevel = NLog.LogLevel;

var builder = WebApplication.CreateBuilder(args);

// ─── Configuration ─────────────────────────────────────────────────────────
BookstoreConfiguration.Initialize(builder.Configuration);
LoadAwsConfiguration(builder.Configuration);

// ─── Logging ───────────────────────────────────────────────────────────────
ConfigureNLog(builder.Configuration);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// ─── Services ──────────────────────────────────────────────────────────────
builder.Services.AddControllersWithViews();

// Entity Framework
var connectionString = BookstoreConfiguration.GetConnectionString("BookstoreDatabaseConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Domain services
builder.Services.AddScoped<IBookService, BookService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReferenceDataService, ReferenceDataService>();
builder.Services.AddScoped<IOfferService, OfferService>();
builder.Services.AddScoped<ICustomerService, CustomerService>();
builder.Services.AddScoped<IAddressService, AddressService>();
builder.Services.AddScoped<IShoppingCartService, ShoppingCartService>();
builder.Services.AddScoped<IImageResizeService, ImageResizeService>();

// Repositories
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IAddressRepository, AddressRepository>();
builder.Services.AddScoped<IBookRepository, BookRepository>();
builder.Services.AddScoped<IOfferRepository, OfferRepository>();
builder.Services.AddScoped<IShoppingCartRepository, ShoppingCartRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddScoped<IReferenceDataRepository, ReferenceDataRepository>();

builder.Services.AddScoped(typeof(IPaginatedList<>), typeof(PaginatedList<>));

// File service
if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
{
    builder.Services.AddSingleton<IAmazonS3, AmazonS3Client>();
    builder.Services.AddScoped<IFileService, S3FileService>();
}
else
{
    builder.Services.AddSingleton<IFileService>(sp =>
    {
        var env = sp.GetRequiredService<IWebHostEnvironment>();
        var webRootPath = Path.Combine(env.WebRootPath ?? env.ContentRootPath, "Content");
        return new LocalFileService(webRootPath);
    });
}

// Image validation service
if (BookstoreConfiguration.GetSetting("Services/ImageValidationService") == "aws")
{
    builder.Services.AddSingleton<IAmazonRekognition, AmazonRekognitionClient>();
    builder.Services.AddScoped<IImageValidationService, RekognitionImageValidationService>();
}
else
{
    builder.Services.AddScoped<IImageValidationService, LocalImageValidationService>();
}

// Authentication
ConfigureAuthentication(builder);

// ─── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// Ensure DB is created (EnsureCreated applies HasData seeding on first run)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    db.Database.EnsureCreated();
}

// ─── Middleware pipeline ────────────────────────────────────────────────────
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthentication();

// Local authentication middleware (when not using AWS Cognito)
if (BookstoreConfiguration.GetSetting("Services/Authentication") != "aws")
{
    builder.Services.AddScoped<LocalAuthenticationMiddleware>();
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

// ─── Local methods ──────────────────────────────────────────────────────────

static void LoadAwsConfiguration(IConfiguration configuration)
{
    var rootPath = "/" + Constants.AppName;

    if (BookstoreConfiguration.GetSetting("Services/Database") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParameterRequest
        {
            Name = $"{rootPath}/Database/ConnectionStrings/BookstoreDatabaseConnection"
        };
        var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
        BookstoreConfiguration.AddSetting(
            response.Parameter.Name.Replace($"{rootPath}/Database/", string.Empty),
            response.Parameter.Value);
    }

    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest { Path = $"{rootPath}/Authentication/", Recursive = true };
        var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
        foreach (var parameter in response.Parameters)
        {
            BookstoreConfiguration.AddSetting(
                parameter.Name.Replace($"{rootPath}/", string.Empty),
                parameter.Value);
        }
    }

    if (BookstoreConfiguration.GetSetting("Services/FileService") == "aws")
    {
        using var client = new AmazonSimpleSystemsManagementClient();
        var request = new GetParametersByPathRequest { Path = $"{rootPath}/Files/", Recursive = true };
        var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
        foreach (var parameter in response.Parameters)
        {
            BookstoreConfiguration.AddSetting(
                parameter.Name.Replace($"{rootPath}/", string.Empty),
                parameter.Value);
        }
    }
}

static void ConfigureNLog(IConfiguration configuration)
{
    var config = new LoggingConfiguration();

    NLog.Targets.Target loggingTarget;

    if (BookstoreConfiguration.GetSetting("Services/LoggingService") == "aws")
    {
        loggingTarget = new AWSTarget { LogGroup = Constants.AppName };
    }
    else
    {
        loggingTarget = new DebuggerTarget("debugger");
    }

    config.AddTarget(loggingTarget);
    config.AddRuleForAllLevels(loggingTarget);

    LogManager.Configuration = config;
}

static void ConfigureAuthentication(WebApplicationBuilder builder)
{
    if (BookstoreConfiguration.GetSetting("Services/Authentication") == "aws")
    {
        builder.Services.AddAuthentication(options =>
        {
            options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
        })
        .AddCookie()
        .AddOpenIdConnect(options =>
        {
            options.ClientId = BookstoreConfiguration.GetSetting("Authentication/Cognito/LocalClientId");
            options.MetadataAddress = BookstoreConfiguration.GetSetting("Authentication/Cognito/MetadataAddress");
            options.ResponseType = OpenIdConnectResponseType.Code;
            options.SaveTokens = true;
            options.Scope.Add("openid");
            options.Scope.Add("profile");
            options.UseTokenLifetime = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "cognito:username",
                RoleClaimType = "cognito:groups"
            };
            options.Events = new OpenIdConnectEvents
            {
                OnRedirectToIdentityProvider = context =>
                {
                    var request = context.Request;
                    context.ProtocolMessage.RedirectUri = $"{request.Scheme}://{request.Host}/signin-oidc";
                    return Task.CompletedTask;
                },
                OnAuthorizationCodeReceived = context =>
                {
                    var request = context.Request;
                    context.TokenEndpointRequest!.RedirectUri = $"{request.Scheme}://{request.Host}/signin-oidc";
                    return Task.CompletedTask;
                },
                OnTokenValidated = async context =>
                {
                    var serviceProvider = context.HttpContext.RequestServices;
                    var service = serviceProvider.GetRequiredService<ICustomerService>();

                    var identity = (ClaimsIdentity?)context.Principal?.Identity;
                    if (identity == null) return;

                    var sub = identity.FindFirst(x => x.Type.Contains("nameidentifier"))?.Value ?? string.Empty;
                    var givenName = identity.FindFirst(x => x.Type.Contains("givenname"))?.Value ?? string.Empty;
                    var surname = identity.FindFirst(x => x.Type.Contains("surname"))?.Value ?? string.Empty;

                    var dto = new CreateOrUpdateCustomerDto(sub, identity.Name ?? string.Empty, givenName, surname);

                    await service.CreateOrUpdateCustomerAsync(dto);
                }
            };
        });
    }
    else
    {
        // Local authentication — no external auth scheme needed; LocalAuthenticationMiddleware handles it
        builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.LoginPath = "/Authentication/Login";
                options.AccessDeniedPath = "/Authentication/Login";
            });

        builder.Services.AddScoped<LocalAuthenticationMiddleware>();
    }

    builder.Services.AddAuthorization();
}
