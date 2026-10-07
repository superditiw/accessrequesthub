using AccessRequestHub.Data;
using AccessRequestHub.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace AccessRequestHub.Middlewares;

public class DemoAuthMiddleware
{
    private readonly RequestDelegate _next;

    public DemoAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db, CurrentUserService currentUserService)
    {
        if (context.Request.Path.StartsWithSegments("/api"))
        {
            var email = context.Request.Headers["X-Demo-User"].FirstOrDefault();
            
            if (string.IsNullOrEmpty(email))
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Extensions = { ["code"] = "MissingAuthHeader" }
                };
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(problem);
                return;
            }

            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status401Unauthorized,
                    Title = "Unauthorized",
                    Extensions = { ["code"] = "UserNotFound" }
                };
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(problem);
                return;
            }

            currentUserService.User = user;
        }

        await _next(context);
    }
}
