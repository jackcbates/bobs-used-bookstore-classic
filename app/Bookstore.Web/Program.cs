using Amazon.Rekognition;
using Amazon.S3;
using Autofac;
using Autofac.Extensions.DependencyInjection;
using BobsBookstoreClassic.Data;
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
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using NLog;
using NLog.AWS.Logger;
using NLog.Config;
using NLog.Targets;
using NLog.Web;
using System;
using System.IO;

var builder = WebApplication.CreateBuilder(args);

// Configure NLog
ConfigureLogging(builder.Configuration);
builder.Logging.ClearProviders();
builder.Host.UseNLog();

// Populate BookstoreConfiguration from IConfiguration
BookstoreConfiguration.Configure(builder.Configuration);

// Configure AWS parameter store if needed
ConfigureConfiguration(builder.Configuration);

// Use Autofac as DI container
builder.Host.UseServiceProviderFactory(new AutofacServiceProviderFactory());

// Add MVC services
builder.Services.AddControllersWithViews();

// Configure EF Core
var connectionString = builder.Configuration.GetConnectionString("BookstoreDatabaseConnection")
    ?? "Server=(localdb)\\MSSQLLocalDB;Initial Catalog=BookStoreClassic;MultipleActiveResultSets=true;Integrated Security=SSPI;";

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// Configure Authentication
var authService = builder.Configuration.GetSection("Services")["Authentication"];
if (authService == "aws")
{
    builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = OpenIdConnectDefaults.AuthenticationScheme;
    })
    .AddCookie()
    .AddOpenIdConnect(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Cognito:LocalClientId"];
        options.MetadataAddress = builder.Configuration["Authentication:Cognito:MetadataAddress"];
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.UseTokenLifetime = false;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "cognito:username",
            RoleClaimType = "cognito:groups"
        };
        options.Events = new OpenIdConnectEvents
        {
            OnRedirectToIdentityProvider = ctx =>
            {
                ctx.ProtocolMessage.RedirectUri = ctx.HttpContext.Request.GetReturnUrl();
                return System.Threading.Tasks.Task.CompletedTask;
            },
            OnAuthorizationCodeReceived = ctx =>
            {
                ctx.TokenEndpointRequest!.RedirectUri = ctx.HttpContext.Request.GetReturnUrl();
                return System.Threading.Tasks.Task.CompletedTask;
            },
            OnTokenValidated = async ctx =>
            {
                var service = ctx.HttpContext.RequestServices.GetRequiredService<ICustomerService>();
                var identity = (System.Security.Claims.ClaimsIdentity)ctx.Principal!.Identity!;
                var dto = new CreateOrUpdateCustomerDto(
                    identity.GetSub(),
                    identity.Name ?? string.Empty,
                    identity.FindFirst(c => c.Type.Contains("givenname"))?.Value ?? string.Empty,
                    identity.FindFirst(c => c.Type.Contains("surname"))?.Value ?? string.Empty);
                await service.CreateOrUpdateCustomerAsync(dto);
            }
        };
    });
}
else
{
    builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
        .AddCookie(options =>
        {
            options.LoginPath = "/Authentication/Login";
        });
}

// Register Autofac modules
builder.Host.ConfigureContainer<ContainerBuilder>(containerBuilder =>
{
    containerBuilder.RegisterType<BookService>().As<IBookService>();
    containerBuilder.RegisterType<OrderService>().As<IOrderService>();
    containerBuilder.RegisterType<ReferenceDataService>().As<IReferenceDataService>();
    containerBuilder.RegisterType<OfferService>().As<IOfferService>();
    containerBuilder.RegisterType<CustomerService>().As<ICustomerService>();
    containerBuilder.RegisterType<AddressService>().As<IAddressService>();
    containerBuilder.RegisterType<ShoppingCartService>().As<IShoppingCartService>();
    containerBuilder.RegisterType<ImageResizeService>().As<IImageResizeService>();

    containerBuilder.RegisterType<CustomerRepository>().As<ICustomerRepository>();
    containerBuilder.RegisterType<AddressRepository>().As<IAddressRepository>();
    containerBuilder.RegisterType<BookRepository>().As<IBookRepository>();
    containerBuilder.RegisterType<OfferRepository>().As<IOfferRepository>();
    containerBuilder.RegisterType<ShoppingCartRepository>().As<IShoppingCartRepository>();
    containerBuilder.RegisterType<OrderRepository>().As<IOrderRepository>();
    containerBuilder.RegisterType<ReferenceDataRepository>().As<IReferenceDataRepository>();

    containerBuilder.RegisterGeneric(typeof(PaginatedList<>)).As(typeof(IPaginatedList<>)).InstancePerLifetimeScope();

    var fileService = builder.Configuration.GetSection("Services")["FileService"];
    if (fileService == "aws")
    {
        containerBuilder.RegisterType<AmazonS3Client>().As<IAmazonS3>();
        containerBuilder.RegisterType<S3FileService>().As<IFileService>();
    }
    else
    {
        var webRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        containerBuilder.RegisterInstance(new LocalFileService(webRootPath)).As<IFileService>();
    }

    var imageValidationService = builder.Configuration.GetSection("Services")["ImageValidationService"];
    if (imageValidationService == "aws")
    {
        containerBuilder.RegisterType<AmazonRekognitionClient>().As<IAmazonRekognition>();
        containerBuilder.RegisterType<RekognitionImageValidationService>().As<IImageValidationService>();
    }
    else
    {
        containerBuilder.RegisterType<LocalImageValidationService>().As<IImageValidationService>();
    }

    if (authService != "aws")
    {
        containerBuilder.RegisterType<LocalAuthenticationMiddleware>();
    }
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Error/Support");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

if (authService != "aws")
{
    app.UseMiddleware<LocalAuthenticationMiddleware>();
}

app.MapControllerRoute(
    name: "Admin_default",
    pattern: "Admin/{controller=Dashboard}/{action=Index}/{id?}",
    defaults: new { area = "Admin" },
    constraints: new { },
    dataTokens: new { area = "Admin" })
    .RequireAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static void ConfigureLogging(IConfiguration configuration)
{
    var config = new LoggingConfiguration();
    Target loggingTarget;

    if (configuration.GetSection("Services")["LoggingService"] == "aws")
    {
        loggingTarget = new AWSTarget { LogGroup = Bookstore.Common.Constants.AppName };
    }
    else
    {
        loggingTarget = new DebuggerTarget();
    }

    config.AddTarget("logging", loggingTarget);
    config.LoggingRules.Add(new LoggingRule("*", NLog.LogLevel.Info, loggingTarget));
    LogManager.Configuration = config;
}

static void ConfigureConfiguration(IConfiguration configuration)
{
    var rootPath = "/" + Bookstore.Common.Constants.AppName;
    const string databasePath = "/Database";
    const string authenticationPath = "/Authentication";
    const string fileServicePath = "/Files";

    if (configuration.GetSection("Services")["Database"] == "aws")
    {
        try
        {
            using var client = new Amazon.SimpleSystemsManagement.AmazonSimpleSystemsManagementClient();
            var request = new Amazon.SimpleSystemsManagement.Model.GetParameterRequest
            {
                Name = $"{rootPath}{databasePath}/ConnectionStrings/BookstoreDatabaseConnection"
            };
            var response = client.GetParameterAsync(request).GetAwaiter().GetResult();
            BookstoreConfiguration.AddSetting(
                response.Parameter.Name.Replace($"{rootPath}{databasePath}/", string.Empty),
                response.Parameter.Value);
        }
        catch (Exception) { }
    }

    if (configuration.GetSection("Services")["Authentication"] == "aws")
    {
        try
        {
            using var client = new Amazon.SimpleSystemsManagement.AmazonSimpleSystemsManagementClient();
            var request = new Amazon.SimpleSystemsManagement.Model.GetParametersByPathRequest
            {
                Path = $"{rootPath}{authenticationPath}/",
                Recursive = true
            };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(
                    parameter.Name.Replace($"{rootPath}/", string.Empty),
                    parameter.Value);
            }
        }
        catch (Exception) { }
    }

    if (configuration.GetSection("Services")["FileService"] == "aws")
    {
        try
        {
            using var client = new Amazon.SimpleSystemsManagement.AmazonSimpleSystemsManagementClient();
            var request = new Amazon.SimpleSystemsManagement.Model.GetParametersByPathRequest
            {
                Path = $"{rootPath}{fileServicePath}/",
                Recursive = true
            };
            var response = client.GetParametersByPathAsync(request).GetAwaiter().GetResult();
            foreach (var parameter in response.Parameters)
            {
                BookstoreConfiguration.AddSetting(
                    parameter.Name.Replace($"{rootPath}/", string.Empty),
                    parameter.Value);
            }
        }
        catch (Exception) { }
    }
}
