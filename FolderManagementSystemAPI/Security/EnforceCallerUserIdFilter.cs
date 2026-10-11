using System.Security.Claims;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FolderManagementSystemAPI.Security;

/// <summary>
/// Blocks impersonation: teacherUserId / hodUserId in query, route, form, or body must match the JWT user,
/// unless the caller is Admin/SectionAdmin.
/// HOD may pass other teachers' teacherUserId (permission management), but hodUserId must still match.
/// </summary>
public sealed class EnforceCallerUserIdFilter : IAsyncActionFilter
{
    private readonly GraderAccessService _graderAccess;

    public EnforceCallerUserIdFilter(GraderAccessService graderAccess)
    {
        _graderAccess = graderAccess;
    }

    private static readonly string[] UserIdKeys =
    {
        "teacherUserId",
        "hodUserId",
        "TeacherUserId",
        "HodUserId"
    };

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var user = context.HttpContext.User;
        if (user?.Identity?.IsAuthenticated != true)
        {
            await next();
            return;
        }

        var role = user.FindFirstValue(JwtTokenService.RoleClaim)
            ?? user.FindFirstValue(ClaimTypes.Role)
            ?? string.Empty;

        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase)
            || role.Equals("SectionAdmin", StringComparison.OrdinalIgnoreCase))
        {
            await next();
            return;
        }

        var claimUserId = user.FindFirstValue(JwtTokenService.UserIdClaim)
            ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(ClaimTypes.Name);

        if (!int.TryParse(claimUserId, out var authenticatedUserId) || authenticatedUserId <= 0)
        {
            context.Result = new UnauthorizedObjectResult(new
            {
                message = "Invalid authentication token (missing user id)."
            });
            return;
        }

        var isHod = role.Equals("HOD", StringComparison.OrdinalIgnoreCase);
        var isGrader = role.Equals("Grader", StringComparison.OrdinalIgnoreCase);

        foreach (var key in UserIdKeys)
        {
            if (!TryGetInt(context, key, out var requestedUserId))
            {
                continue;
            }

            // HOD manages teacher file permissions for other users — do not treat
            // route/body teacherUserId as self-impersonation.
            if (isHod && IsTeacherUserIdKey(key))
            {
                continue;
            }

            if (requestedUserId == authenticatedUserId)
            {
                continue;
            }

            if (isGrader && IsTeacherUserIdKey(key)
                && await _graderAccess.CanActAsTeacherAsync(authenticatedUserId, requestedUserId))
            {
                continue;
            }

            context.Result = new ObjectResult(new
            {
                message = "You cannot act as another user. The authenticated user does not match the requested user id."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
            return;
        }

        await next();
    }

    private static bool IsTeacherUserIdKey(string key) =>
        key.Equals("teacherUserId", StringComparison.OrdinalIgnoreCase)
        || key.Equals("TeacherUserId", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetInt(ActionExecutingContext context, string key, out int value)
    {
        value = 0;

        if (context.HttpContext.Request.Query.TryGetValue(key, out var queryValue) &&
            int.TryParse(queryValue.FirstOrDefault(), out value))
        {
            return true;
        }

        if (context.HttpContext.Request.HasFormContentType)
        {
            var form = context.HttpContext.Request.Form;
            if (form.TryGetValue(key, out var formValue) &&
                int.TryParse(formValue.FirstOrDefault(), out value))
            {
                return true;
            }
        }

        if (context.ActionArguments.TryGetValue(key, out var arg) && arg != null)
        {
            if (arg is int i)
            {
                value = i;
                return true;
            }

            if (int.TryParse(arg.ToString(), out value))
            {
                return true;
            }
        }

        // DTO bodies: TeacherUserId / HodUserId properties
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument == null) continue;
            var type = argument.GetType();
            if (type == typeof(string) || type.IsPrimitive) continue;

            var prop = type.GetProperty(key)
                ?? type.GetProperty(key, System.Reflection.BindingFlags.IgnoreCase | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (prop == null) continue;

            var propValue = prop.GetValue(argument);
            if (propValue == null) continue;
            if (propValue is int pi)
            {
                value = pi;
                return true;
            }

            if (int.TryParse(propValue.ToString(), out value))
            {
                return true;
            }
        }

        return false;
    }
}
