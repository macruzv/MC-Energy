using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Controllers
{
    [Authorize]
    public class TagGroupsController : BaseController
    {
        public TagGroupsController(
            UserManager userManager,
            ILoggerFactory loggerFactory,
            Microsoft.Extensions.Configuration.IConfiguration config,
            OCPPCoreContext dbContext)
            : base(userManager, loggerFactory, config, dbContext)
        {
            Logger = loggerFactory.CreateLogger<TagGroupsController>();
        }

        public async Task<IActionResult> Index(string returnUrl = null)
        {
            DbContext.CheckDatabase();
            if (!User.IsInRole(Constants.AdminRoleName)) return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            var groups = await DbContext.TagGroups.OrderBy(g => g.Name).ToListAsync();
            return View(groups);
        }

        public IActionResult Create(string returnUrl = null)
        {
            if (!User.IsInRole(Constants.AdminRoleName)) return RedirectToAction("Index", "Home");
            ViewBag.ReturnUrl = returnUrl;
            return View("Edit", new TagGroup());
        }

        public async Task<IActionResult> Edit(int id, string returnUrl = null)
        {
            if (!User.IsInRole(Constants.AdminRoleName)) return RedirectToAction("Index", "Home");

            ViewBag.ReturnUrl = returnUrl;
            var group = await DbContext.TagGroups.FindAsync(id);
            if (group == null) return NotFound();

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save(TagGroup group, string returnUrl = null)
        {
            if (!User.IsInRole(Constants.AdminRoleName)) return Unauthorized();

            if (ModelState.IsValid)
            {
                if (group.TagGroupId == 0)
                {
                    DbContext.TagGroups.Add(group);
                }
                else
                {
                    DbContext.Update(group);
                }
                await DbContext.SaveChangesAsync();

                if (!string.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
                return RedirectToAction(nameof(Index));
            }
            ViewBag.ReturnUrl = returnUrl;
            return View("Edit", group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            if (!User.IsInRole(Constants.AdminRoleName)) return Unauthorized();

            var group = await DbContext.TagGroups.FindAsync(id);
            if (group != null)
            {
                // No permitir borrar el grupo 'General' si es el único o si se prefiere protegerlo
                if (group.Name.Equals("General", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["ErrorMessage"] = "No se puede eliminar el grupo General.";
                    return RedirectToAction(nameof(Index));
                }

                DbContext.TagGroups.Remove(group);
                await DbContext.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
