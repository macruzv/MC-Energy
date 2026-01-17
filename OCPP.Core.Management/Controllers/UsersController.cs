using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class UsersController : BaseController
    {
        public UsersController(
            UserManager userManager,
            Microsoft.Extensions.Logging.ILoggerFactory loggerFactory,
            Microsoft.Extensions.Configuration.IConfiguration config,
            OCPPCoreContext dbContext)
            : base(userManager, loggerFactory, config, dbContext)
        {
        }

        public async Task<IActionResult> Index()
        {
            DbContext.CheckDatabase();
            var users = await DbContext.Users
                .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                .Include(u => u.UserChargePoints)
                .OrderBy(u => u.Username)
                .ToListAsync();
            return View(users);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Roles = await DbContext.Roles.ToListAsync();
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(User user, int[] selectedRoles)
        {
            if (ModelState.IsValid)
            {
                user.CreateDateTime = DateTime.Now;
                user.IsActive = true;
                DbContext.Users.Add(user);
                await DbContext.SaveChangesAsync();

                foreach (var roleId in selectedRoles)
                {
                    DbContext.UserRoles.Add(new UserRole { UserId = user.UserId, RoleId = roleId });
                }
                await DbContext.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            ViewBag.Roles = await DbContext.Roles.ToListAsync();
            return View(user);
        }

        public async Task<IActionResult> Assignments(int id)
        {
            var user = await DbContext.Users
                .Include(u => u.UserChargePoints)
                .FirstOrDefaultAsync(u => u.UserId == id);
            
            if (user == null) return NotFound();

            ViewBag.AllChargePoints = await DbContext.ChargePoints.ToListAsync();
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> SaveAssignments(int userId, string[] selectedChargePoints)
        {
            var currentAssignments = DbContext.UserChargePoints.Where(ucp => ucp.UserId == userId);
            DbContext.UserChargePoints.RemoveRange(currentAssignments);

            if (selectedChargePoints != null)
            {
                foreach (var cpId in selectedChargePoints)
                {
                    DbContext.UserChargePoints.Add(new UserChargePoint { UserId = userId, ChargePointId = cpId });
                }
            }

            await DbContext.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await DbContext.Users.FindAsync(id);
            if (user != null)
            {
                DbContext.Users.Remove(user);
                await DbContext.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
