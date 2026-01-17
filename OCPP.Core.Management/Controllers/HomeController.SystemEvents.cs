using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database; // Assuming this is where DbContext comes from
using OCPP.Core.Management.Models;
using ClosedXML.Excel;
using System.IO;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController
    {
        // Assuming DbContext is available, e.g., via constructor injection
        // private readonly ApplicationDbContext DbContext;
        // private readonly ILogger<HomeController> Logger; // Assuming Logger is available

        [Authorize]
        public IActionResult SystemEvents(DateTime? startDate, DateTime? endDate, string chargePointId, string result, int? connectorId, int page = 1, int pageSize = 50)
        {
            var viewModel = new SystemEventsViewModel();

            // Defaults
            if (!startDate.HasValue) startDate = DateTime.UtcNow.AddDays(-1);
            if (!endDate.HasValue) endDate = DateTime.UtcNow;

            viewModel.StartDate = startDate.Value;
            viewModel.EndDate = endDate.Value;
            viewModel.SelectedChargePointId = chargePointId;
            viewModel.SelectedResult = result;
            viewModel.SelectedConnectorId = connectorId;
            viewModel.PageSize = pageSize;

            // Load filters
            viewModel.ChargePointList = DbContext.ChargePoints.Select(cp => new SelectListItem { Value = cp.ChargePointId, Text = cp.Name ?? cp.ChargePointId }).ToList();
            viewModel.ChargePointList.Insert(0, new SelectListItem { Value = "", Text = "Todos" });

            viewModel.ResultList = DbContext.MessageLogs.Select(m => m.Result).Distinct().Where(r => !string.IsNullOrEmpty(r)).Select(r => new SelectListItem { Value = r, Text = r }).ToList();
            viewModel.ResultList.Insert(0, new SelectListItem { Value = "", Text = "Todos" });

            // Connectors (Simplified: 1 to 5) - Original code had hardcoded, new code uses DbContext.Connectors
            // If DbContext.Connectors is not available or desired, revert to hardcoded list.
            viewModel.ConnectorList = DbContext.ConnectorStatuses.Select(c => c.ConnectorId).Distinct().OrderBy(id => id).Select(id => new SelectListItem { Value = id.ToString(), Text = id.ToString() }).ToList();
            viewModel.ConnectorList.Insert(0, new SelectListItem { Text = "Todos", Value = "" });


            // Apply Filters
            var query = GetSystemEventsQuery(startDate, endDate, chargePointId, result, connectorId);

            // Pagination Logic
            viewModel.TotalItems = query.Count();
            viewModel.TotalPages = (int)Math.Ceiling(viewModel.TotalItems / (double)pageSize);
            viewModel.CurrentPage = page;
            
            // Validate page bounds
            if (viewModel.CurrentPage < 1) viewModel.CurrentPage = 1;
            if (viewModel.CurrentPage > viewModel.TotalPages && viewModel.TotalPages > 0) viewModel.CurrentPage = viewModel.TotalPages;

            viewModel.Events = query
                .OrderByDescending(m => m.LogId)
                .Skip((viewModel.CurrentPage - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(viewModel);
        }

        [Authorize]
        public IActionResult ExportSystemEvents(DateTime? startDate, DateTime? endDate, string chargePointId, string result, int? connectorId)
        {
            try
            {
                var query = GetSystemEventsQuery(startDate, endDate, chargePointId, result, connectorId);
                var events = query.OrderByDescending(m => m.LogId).Take(5000).ToList(); // Export limit

                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add("SystemEvents");

                // Headers
                worksheet.Cell(1, 1).Value = "Fecha";
                worksheet.Cell(1, 2).Value = "Cargador";
                worksheet.Cell(1, 3).Value = "Conector";
                worksheet.Cell(1, 4).Value = "Mensaje";
                worksheet.Cell(1, 5).Value = "Resultado";
                worksheet.Cell(1, 6).Value = "Código Error";

                // Data
                int row = 2;
                foreach (var item in events)
                {
                    worksheet.Cell(row, 1).Value = item.LogTime.ToLocalTime();
                    worksheet.Cell(row, 2).Value = item.ChargePointId;
                    worksheet.Cell(row, 3).Value = item.ConnectorId;
                    worksheet.Cell(row, 4).Value = item.Message;
                    worksheet.Cell(row, 5).Value = item.Result;
                    worksheet.Cell(row, 6).Value = item.ErrorCode;
                    row++;
                }

                worksheet.Columns().AdjustToContents();

                using var memoryStream = new MemoryStream();
                workbook.SaveAs(memoryStream);
                memoryStream.Position = 0;

                return File(memoryStream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"SystemEvents_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                // Assuming Logger is available, e.g., injected ILogger
                // Logger.LogError(ex, "ExportSystemEvents: Error exporting data");
                return RedirectToAction("SystemEvents");
            }
        }

        private IQueryable<MessageLog> GetSystemEventsQuery(DateTime? startDate, DateTime? endDate, string chargePointId, string result, int? connectorId)
        {
            var query = DbContext.MessageLogs.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(m => m.LogTime >= startDate.Value.ToUniversalTime());

            if (endDate.HasValue)
                query = query.Where(m => m.LogTime <= endDate.Value.AddDays(1).AddTicks(-1).ToUniversalTime()); // Adjusted to include the whole end day

            if (!string.IsNullOrEmpty(chargePointId))
            {
                query = query.Where(m => m.ChargePointId == chargePointId);
            }

            if (!string.IsNullOrEmpty(result))
            {
                query = query.Where(m => m.Result == result);
            }

            if (connectorId.HasValue)
            {
                query = query.Where(m => m.ConnectorId == connectorId.Value);
            }

            return query;
        }
    }
}
