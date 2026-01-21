using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        public async Task<IActionResult> Diagnostics(string search, string errorCode)
        {
            if (string.IsNullOrEmpty(search) && !string.IsNullOrEmpty(errorCode))
            {
                search = errorCode;
            }
            var query = DbContext.ErrorCatalog.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower();
                query = query.Where(e => e.ErrorCode.ToLower().Contains(search) || 
                                       e.Title.ToLower().Contains(search) || 
                                       e.Description.ToLower().Contains(search));
            }

            var entries = await query.ToListAsync();
            ViewData["Search"] = search;
            return View(entries);
        }
    }
}
