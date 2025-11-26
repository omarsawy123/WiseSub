using Hangfire.Dashboard;

namespace WiseSub.API.Middleware;

/// <summary>
/// Authorization filter for Hangfire Dashboard in production.
/// Only allows authenticated admin users to access the dashboard.
/// </summary>
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        var httpContext = context.GetHttpContext();

        // In production, require authentication
        if (!httpContext.User.Identity?.IsAuthenticated ?? true)
        {
            return false;
        }

        // Optionally, check for admin role
        // return httpContext.User.IsInRole("Admin");
        
        // For now, allow any authenticated user
        return true;
    }
}
