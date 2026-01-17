using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    [Authorize]
    public class CustomersController : BaseController
    {
        public CustomersController(
            UserManager userManager,
            ILoggerFactory loggerFactory,
            IConfiguration config,
            OCPPCoreContext dbContext)
            : base(userManager, loggerFactory, config, dbContext)
        {
            Logger = loggerFactory.CreateLogger<CustomersController>();
        }

        public async Task<IActionResult> Index(string name, string phone)
        {
            try
            {
                ViewBag.SearchName = name;
                ViewBag.SearchPhone = phone;

                var query = DbContext.Customers.Include(c => c.ChargeTags).AsQueryable();

                if (!string.IsNullOrEmpty(name))
                {
                    query = query.Where(c => c.Name.Contains(name));
                }

                if (!string.IsNullOrEmpty(phone))
                {
                    query = query.Where(c => c.Phone.Contains(phone));
                }

                var customers = await query.ToListAsync();
                return View(customers);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error loading customers index");
                return RedirectToAction("Error", "Home");
            }
        }

        public async Task<IActionResult> Create()
        {
            try
            {
                ViewBag.AvailableTags = await DbContext.ChargeTags
                    .Where(t => t.CustomerId == null && (t.Blocked == null || t.Blocked == false))
                    .ToListAsync();
                return View(new Customer());
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Error opening Create Customer view");
                TempData["ErrMessage"] = "Error al abrir la vista de creación: " + ex.Message;
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer, string[] selectedTags)
        {
            if (ModelState.IsValid)
            {
                DbContext.Add(customer);
                await DbContext.SaveChangesAsync();

                if (selectedTags != null)
                {
                    foreach (var tagId in selectedTags)
                    {
                        var tag = await DbContext.ChargeTags.FindAsync(tagId);
                        if (tag != null)
                        {
                            tag.CustomerId = customer.CustomerId;
                        }
                    }
                    await DbContext.SaveChangesAsync();
                }

                return RedirectToAction(nameof(Index));
            }
            
            ViewBag.AvailableTags = await DbContext.ChargeTags
                .Where(t => t.CustomerId == null)
                .ToListAsync();
            return View(customer);
        }

        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var customer = await DbContext.Customers
                .Include(c => c.ChargeTags)
                .FirstOrDefaultAsync(m => m.CustomerId == id);

            if (customer == null) return NotFound();

            ViewBag.AvailableTags = await DbContext.ChargeTags
                .Where(t => (t.CustomerId == null || t.CustomerId == id) && (t.Blocked == null || t.Blocked == false))
                .ToListAsync();

            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Customer customer, string[] selectedTags)
        {
            if (id != customer.CustomerId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    DbContext.Update(customer);
                    
                    // Unlink previous tags
                    var currentTags = await DbContext.ChargeTags.Where(t => t.CustomerId == id).ToListAsync();
                    foreach (var tag in currentTags)
                    {
                        tag.CustomerId = null;
                    }

                    // Link new tags
                    if (selectedTags != null)
                    {
                        foreach (var tagId in selectedTags)
                        {
                            var tag = await DbContext.ChargeTags.FindAsync(tagId);
                            if (tag != null)
                            {
                                tag.CustomerId = customer.CustomerId;
                            }
                        }
                    }

                    await DbContext.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CustomerExists(customer.CustomerId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            
            ViewBag.AvailableTags = await DbContext.ChargeTags
                .Where(t => (t.CustomerId == null || t.CustomerId == id) && (t.Blocked == null || t.Blocked == false))
                .ToListAsync();
            return View(customer);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var customer = await DbContext.Customers.FindAsync(id);
            if (customer != null)
            {
                // Tags will be unlinked automatically due to SetNull configuration or manually
                var tags = await DbContext.ChargeTags.Where(t => t.CustomerId == id).ToListAsync();
                foreach(var t in tags) t.CustomerId = null;
                
                DbContext.Customers.Remove(customer);
                await DbContext.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool CustomerExists(int id)
        {
            return DbContext.Customers.Any(e => e.CustomerId == id);
        }
    }
}
