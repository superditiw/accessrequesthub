using AccessRequestHub.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace AccessRequestHub.Middlewares;

public class GlobalExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is AppException appEx)
        {
            var problemDetails = new ProblemDetails
            {
                Status = appEx.StatusCode,
                Title = appEx.Message,
                Type = $"https://httpstatuses.com/{appEx.StatusCode}",
                Extensions = { ["code"] = appEx.Code }
            };

            httpContext.Response.StatusCode = appEx.StatusCode;
            await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
            return true;
        }

        var genericProblem = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Title = "An unexpected error occurred.",
            Detail = exception.Message,
            Type = "https://httpstatuses.com/500",
            Extensions = { ["code"] = "InternalServerError" }
        };

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(genericProblem, cancellationToken);
        return true;
    }
}
