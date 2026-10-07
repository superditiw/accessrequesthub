using AccessRequestHub.Data;
using AccessRequestHub.Exceptions;
using AccessRequestHub.Models;
using AccessRequestHub.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace AccessRequestHub.Endpoints;

public static class UserEndpoints
{
    public static void MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/me", async (AppDbContext db, CurrentUserService currentUserService) =>
        {
            var user = currentUserService.User;
            if (user == null)
            {
                throw new AppException(401, "Unauthorized", "User is not authenticated");
            }

            var isManager = await db.Users.AnyAsync(u => u.ManagerId == user.Id);
            
            var ownedApps = await db.Applications
                .Where(a => a.SystemOwnerId == user.Id)
                .Select(a => new OwnedApplicationDto(a.Id, a.Name))
                .ToListAsync();

            var response = new UserProfileDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.Role.ToString(),
                user.ManagerId.HasValue,
                isManager,
                ownedApps.ToArray(),
                user.Role == UserRole.Auditor
            );

            return Results.Ok(response);
        });
    }
}

public record OwnedApplicationDto(Guid Id, string Name);

public record UserProfileDto(
    Guid Id,
    string Email,
    string DisplayName,
    string Role,
    bool CanCreate,
    bool IsManager,
    OwnedApplicationDto[] OwnedApplications,
    bool CanViewAll
);
