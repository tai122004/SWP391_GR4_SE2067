using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Web;

namespace RWPM.Common.Attributes
{
    public class RemoveEmptyQueryStringAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var request = filterContext.HttpContext.Request;
            var newQuery = HttpUtility.ParseQueryString(request.QueryString.ToString());

            // Get all keys with empty string values
            var keysToRemove = newQuery.AllKeys
                .Where(k => string.IsNullOrEmpty(newQuery[k]))
                .ToList();

            // Check if there are any keys to remove
            if (keysToRemove.Any())
            {
                // Remove the identified keys
                foreach (var key in keysToRemove)
                {
                    newQuery.Remove(key);
                }

                // Reconstruct the URL without empty query string values
                var newUrl = $"{request.Path}?{newQuery}";

                // Redirect the user to the new URL
                filterContext.Result = new RedirectResult(newUrl);
            }

            base.OnActionExecuting(filterContext);
        }
    }

}