using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using YoJanus.Web.Models;

namespace YoJanus.Web.Auth
{
    public class YoAuthorizeAttribute : Attribute, IAuthorizationFilter
    {
        // not used yet but we can extend here
        private readonly string[] _requiredPermissions;

        public YoAuthorizeAttribute(params string[] requiredPermissions) => this._requiredPermissions = requiredPermissions.ToArray();

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<YoAuthorizeAttribute>>();
            var userId = context.HttpContext.User.UserId();
            logger.LogDebug("attempting to authorize: {action} for userid: {userId}", context.ActionDescriptor.DisplayName, userId);

            var email = context.HttpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
            // eventually we can do something more sophisticated, check against a role/permission table or something
            // for now just checking email ends with @yojanus.com
            if (email == null || !email.EndsWith("@yojanus.com"))
                context.Result = new ForbidResult(context.HttpContext.User.Identity.AuthenticationType);

            // else do nothing to allow normal processing to continue
        }
    }
}