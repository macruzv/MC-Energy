using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class RolesController : Controller
    {
        private readonly OCPPCoreContext _context;

        public RolesController(OCPPCoreContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var roles = await _context.Roles
                .Include(r => r.RolePermissions)
                .ThenInclude(rp => rp.Permission)
                .ToListAsync();
            return View(roles);
        }

        public async Task<IActionResult> Permissions(int id)
        {
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleId == id);

            if (role == null) return NotFound();

            var allPermissions = await _context.Permissions.ToListAsync();
            ViewBag.AllPermissions = allPermissions;
            ViewBag.AssignedPermissionIds = role.RolePermissions.Select(rp => rp.PermissionId).ToList();

            return View(role);
        }

        [HttpPost]
        public async Task<IActionResult> SavePermissions(int roleId, int[] permissionIds)
        {
            var role = await _context.Roles
                .Include(r => r.RolePermissions)
                .FirstOrDefaultAsync(r => r.RoleId == roleId);

            if (role == null) return NotFound();

            // Eliminar permisos actuales
            _context.RolePermissions.RemoveRange(role.RolePermissions);
            
            // Agregar nuevos permisos
            if (permissionIds != null)
            {
                foreach (var pId in permissionIds)
                {
                    _context.RolePermissions.Add(new RolePermission 
                    { 
                        RoleId = roleId, 
                        PermissionId = pId 
                    });
                }
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
