#nullable enable
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware : IMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";

        private readonly ICustomerService _customerService;

        public LocalAuthenticationMiddleware(ICustomerService customerService)
        {
            _customerService = customerService;
        }

        public async Task InvokeAsync(HttpContext context, RequestDelegate next)
        {
            if (context.Request.Path.Value != null &&
                context.Request.Path.Value.StartsWith("/Authentication/Login", StringComparison.OrdinalIgnoreCase))
            {
                CreateClaimsPrincipal(context);
                await SaveCustomerDetailsAsync(context);

                context.Response.Cookies.Append("LocalAuthentication", "true", new CookieOptions
                {
                    Expires = DateTimeOffset.Now.AddDays(1),
                    HttpOnly = true,
                    SameSite = SameSiteMode.Lax
                });

                context.Response.Redirect("/");
            }
            else if (context.Request.Cookies["LocalAuthentication"] != null)
            {
                CreateClaimsPrincipal(context);
                await SaveCustomerDetailsAsync(context);
                await next(context);
            }
            else
            {
                await next(context);
            }
        }

        private static void CreateClaimsPrincipal(HttpContext context)
        {
            var identity = new ClaimsIdentity("Application");

            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));

            context.User = new ClaimsPrincipal(identity);
        }

        private async Task SaveCustomerDetailsAsync(HttpContext context)
        {
            var identity = (ClaimsIdentity?)context.User.Identity;
            if (identity == null) return;

            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier")?.Value ?? string.Empty,
                identity.Name ?? string.Empty,
                identity.FindFirst("given_name")?.Value ?? string.Empty,
                identity.FindFirst("family_name")?.Value ?? string.Empty);

            await _customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
