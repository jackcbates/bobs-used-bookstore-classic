using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Bookstore.Domain.Customers;
using Microsoft.AspNetCore.Http;

namespace Bookstore.Web.Helpers
{
    public class LocalAuthenticationMiddleware
    {
        private const string UserId = "FB6135C7-1464-4A72-B74E-4B63D343DD09";

        private readonly RequestDelegate _next;

        public LocalAuthenticationMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, ICustomerService customerService)
        {
            if (context.Request.Path.StartsWithSegments("/Authentication/Login"))
            {
                var identity = CreateClaimsIdentity();
                var principal = new ClaimsPrincipal(identity);
                context.User = principal;

                await SaveCustomerDetailsAsync(customerService, identity);

                context.Response.Cookies.Append("LocalAuthentication", "", new CookieOptions
                {
                    Expires = DateTime.Now.AddDays(1)
                });

                context.Response.Redirect("/");
                return;
            }
            else if (context.Request.Cookies.ContainsKey("LocalAuthentication"))
            {
                var identity = CreateClaimsIdentity();
                var principal = new ClaimsPrincipal(identity);
                context.User = principal;

                await SaveCustomerDetailsAsync(customerService, identity);
            }

            await _next(context);
        }

        private static ClaimsIdentity CreateClaimsIdentity()
        {
            var identity = new ClaimsIdentity("Application");

            identity.AddClaim(new Claim(ClaimTypes.Name, "bookstoreuser"));
            identity.AddClaim(new Claim("nameidentifier", UserId));
            identity.AddClaim(new Claim("given_name", "Bookstore"));
            identity.AddClaim(new Claim("family_name", "User"));
            identity.AddClaim(new Claim(ClaimTypes.Role, "Administrators"));

            return identity;
        }

        private static async Task SaveCustomerDetailsAsync(ICustomerService customerService, ClaimsIdentity identity)
        {
            var dto = new CreateOrUpdateCustomerDto(
                identity.FindFirst("nameidentifier").Value,
                identity.Name,
                identity.FindFirst("given_name").Value,
                identity.FindFirst("family_name").Value);

            await customerService.CreateOrUpdateCustomerAsync(dto);
        }
    }
}
