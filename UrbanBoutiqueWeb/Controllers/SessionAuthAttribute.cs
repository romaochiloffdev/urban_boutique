using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace UrbanBoutiqueWeb.Controllers
{
    public class SessionAuthAttribute : ActionFilterAttribute
    {
        public string? Role { get; set; }

        public override void OnActionExecuting(ActionExecutingContext context)
        {
            var session = context.HttpContext.Session;
            var username = session.GetString("Username");
            var role = session.GetString("Role");

            if (string.IsNullOrEmpty(username))
            {
                context.Result = new UnauthorizedObjectResult(new { message = "Authentication required" });
                return;
            }

            if (!string.IsNullOrEmpty(Role) && role != Role)
            {
                context.Result = new ObjectResult(new { message = "Forbidden" }) { StatusCode = 403 };
                return;
            }
        }
    }
}
