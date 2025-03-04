using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace activity_dashboard.Filters
{
    public class AuthenticationFilter : IAuthorizationFilter
    {
        public void OnAuthorization(AuthorizationFilterContext context)
        {
            var accessToken = context.HttpContext.Session.GetString("AccessToken");
            
            if (string.IsNullOrEmpty(accessToken))
            {
                context.Result = new RedirectToActionResult("Login", "Account", null);
            }
        }
    }
}