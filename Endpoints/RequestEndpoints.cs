using AccessRequestHub.DTOs;
using AccessRequestHub.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace AccessRequestHub.Endpoints;

public static class RequestEndpoints
{
    public static void MapRequestEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/requests", async ([FromBody] CreateRequestDto payload, AccessRequestService accessRequestService) =>
        {
            var (request, isNew) = await accessRequestService.CreateRequestAsync(payload);
            
            var response = new AccessRequestResponseDto(
                request.Id,
                request.ClientRequestId,
                request.RequesterId,
                request.ApplicationId,
                request.Environment.ToString(),
                request.AccessLevel.ToString(),
                request.Justification,
                request.IsHighRisk,
                request.Status.ToString(),
                request.PolicyVersionCode,
                request.Version,
                request.CreatedAt,
                request.UpdatedAt
            );

            if (isNew)
            {
                return Results.Created($"/api/requests/{request.Id}", response);
            }
            
            return Results.Ok(response);
        });
    }
}
