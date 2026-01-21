using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Filters
{
    public class PermissionFilter : IAsyncActionFilter
    {
        private readonly OCPPCoreContext _db;
        private readonly ILogger<PermissionFilter> _logger;

        public PermissionFilter(OCPPCoreContext db, ILogger<PermissionFilter> logger)
        {
            _db = db;
            _logger = logger;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var controller = context.RouteData.Values["controller"]?.ToString();
            var action = context.RouteData.Values["action"]?.ToString();

            // 1. Exenciones (Acceso público o básico)
            if (controller == "Account" || action == "AccessDenied" || (controller == "Home" && action == "Error") || (controller == "Home" && action == "Landing"))
            {
                await next();
                return;
            }

            // 2. Verificar si el usuario está autenticado
            if (context.HttpContext.User?.Identity?.IsAuthenticated != true)
            {
                // Dejar que el middleware de Authorization maneje el redireccionamiento a Login
                await next();
                return;
            }

            // 2.b. Administradores tienen acceso total por defecto
            if (context.HttpContext.User.IsInRole(Constants.AdminRoleName))
            {
                await next();
                return;
            }

            // 3. Obtener el ID del usuario
            var userIdStr = context.HttpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(userIdStr, out int userId))
            {
                _logger.LogInformation("PermissionFilter: Checking access for User {0} to {1}/{2}", userId, controller, action);

                // Verificar si el usuario aún existe en la base de datos (evita errores tras cambio de DB a SQLite)
                var userExists = await _db.Users.AnyAsync(u => u.UserId == userId);
                if (!userExists)
                {
                    _logger.LogWarning("PermissionFilter: User {0} not found in DB, logging out.", userId);
                    context.Result = new RedirectToActionResult("Logout", "Account", null);
                    return;
                }

                // 4. Verificar si tiene el permiso para este controlador y acción
                // Usamos Joins explícitos para mayor confiabilidad en la traducción a SQL
                var hasPermission = await _db.UserRoles
                    .Where(ur => ur.UserId == userId)
                    .Join(_db.RolePermissions, ur => ur.RoleId, rp => rp.RoleId, (ur, rp) => rp)
                    .Join(_db.Permissions, rp => rp.PermissionId, p => p.PermissionId, (rp, p) => p)
                    .AnyAsync(p => p.Controller.ToLower() == controller.ToLower() && 
                                   (string.IsNullOrEmpty(p.Action) || p.Action.ToLower() == action.ToLower()));

                _logger.LogInformation("PermissionFilter: User {0} hasPermission={1}", userId, hasPermission);

                if (hasPermission)
                {
                    await next();
                    return;
                }
            }

            _logger.LogWarning("PermissionFilter: ACCESS DENIED for User {0} to {1}/{2}", userIdStr, controller, action);

            // 5. Si no tiene permiso, redirigir o devolver Forbid
            context.Result = new ForbidResult();
            
            // Opcional: Redirigir a una vista amable de "Acceso Denegado"
            // context.Result = new RedirectToActionResult("AccessDenied", "Account", null);
        }
    }
}
