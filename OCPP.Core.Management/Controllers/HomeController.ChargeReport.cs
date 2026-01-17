using System;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OCPP.Core.Management.Models;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using ClosedXML.Excel;
using System.IO;
using System.Text;
using System.Collections.Generic;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult ChargeReport(DateTime? startDate, DateTime? stopDate, string group)
        {
            try
            {
                Logger.LogTrace("ChargeReport: GenerateReport()...");
                var report = GenerateReport(startDate, stopDate, group);
                return View(report);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error loading charge points from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        // ... methods ChargeReportCsv, ChargeReportXlsx, etc need updates too or default null
        // I'll update them later or now? The interface only calls ChargeReport for the view.
        // But the CSV/XLSX buttons in the view will need the group param too.
        // I should update those signatures too.
        
        [Authorize]
        public IActionResult ChargeReportCsv(DateTime? startDate, DateTime? stopDate, string group)
        {
            try
            {
                Logger.LogTrace("ChargeReport: ChargeReportCsv()...");
                var report = GenerateReport(startDate, stopDate, group);
                // ... (rest is same, just using filtered report)
                var csv = new StringBuilder();

                csv.Append(_localizer["ChargeReportGroup"]);
                csv.Append(DefaultCSVSeparator);
                csv.Append(_localizer["ChargeReportTag"]);
                csv.Append(DefaultCSVSeparator);
                csv.AppendLine(_localizer["ChargeReportEnergy"]);

                foreach (var grp in report.Groups)
                {
                    foreach (var tag in grp.Tags)
                    {
                        var totalEnergy = tag.Transactions
                            .Where(t => t.Energy.HasValue)
                            .Sum(t => t.Energy.Value);
                        csv.Append(EscapeCsvValue(grp.GroupName, DefaultCSVSeparator));
                        csv.Append(DefaultCSVSeparator);
                        csv.Append(EscapeCsvValue(tag.TagName, DefaultCSVSeparator));
                        csv.Append(DefaultCSVSeparator);
                        csv.Append(Math.Round(totalEnergy, 3));
                        csv.AppendLine();
                    }
                }

                var fileName = $"ChargeReport_{DateTime.Now:yyyyMMddHHmmss}.csv";
                return File(Encoding.GetEncoding("ISO-8859-1").GetBytes(csv.ToString()), "text/csv", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating CSV report");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        [Authorize]
        public IActionResult ChargeReportXlsx(DateTime? startDate, DateTime? stopDate, string group)
        {
             try
            {
                Logger.LogTrace("ChargeReport: ChargeReportXslx()...");
                var report = GenerateReport(startDate, stopDate, group);
                using var workbook = new XLWorkbook();
                var worksheet = workbook.Worksheets.Add(_localizer["ChargeReport"]);

                worksheet.Cell(1, 1).Value = _localizer["ChargeReportGroup"].ToString();
                worksheet.Cell(1, 2).Value = _localizer["ChargeReportTag"].ToString();
                worksheet.Cell(1, 3).Value = _localizer["ChargeReportEnergy"].ToString();

                var row = 2;
                foreach (var grp in report.Groups)
                {
                    foreach (var tag in grp.Tags)
                    {
                        var totalEnergy = tag.Transactions
                            .Where(t => t.Energy.HasValue)
                            .Sum(t => t.Energy.Value);

                        worksheet.Cell(row, 1).Value = grp.GroupName;
                        worksheet.Cell(row, 2).Value = tag.TagName;
                        worksheet.Cell(row, 3).Value = Math.Round(totalEnergy, 3);
                        row++;
                    }
                }

                worksheet.Columns().AdjustToContents(); // Auto-scaling the column width

                using var stream = new MemoryStream();
                workbook.SaveAs(stream);
                var content = stream.ToArray();
                var fileName = $"ChargeReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error generating XLSX report");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }

        // ... skipping AllTransactionsCsv updates for now as they use GetAllTransactions, I should update that too if I want it consistent?
        // The user only asked for the Charge Report "View" to be updated. But logically the exports should match.
        // I will focus on ChargeReport/GenerateReport first.

        private ChargeReportViewModel GenerateReport(DateTime? startDate, DateTime? stopDate, string group)
        {
            Logger.LogTrace("ChargeReport: GenerateReport({0}, {1}, {2})", startDate?.ToString("s"), stopDate?.ToString("s"), group);

            startDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1); // Default to first day of previous month
            stopDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(-1); // Default to last day of previous month

            // Restrict DateTime to date
            startDate = startDate.Value.Date;
            stopDate = stopDate.Value.Date;

            // Timestamps in DB are UTC
            DateTime dbStartDate = startDate.Value.ToUniversalTime();
            // Stop date => use next day and compare with "<" (no clock times needed)
            DateTime dbStopDate = stopDate.Value.AddDays(1).ToUniversalTime();

            // Load available groups for dropdown
            var availableGroups = DbContext.ChargeTags
                .Where(ct => !string.IsNullOrEmpty(ct.ParentTagId))
                .Select(ct => ct.ParentTagId)
                .Distinct()
                .OrderBy(g => g)
                .ToList();

            // Load transactions with LEFT JOIN charge tags
            // Load transactions with LEFT JOIN charge tags
            var transactionsQuery = (from t in DbContext.Transactions
                            join startCT in DbContext.ChargeTags on t.StartTagId equals startCT.TagId into ft_tmp
                            from startCT in ft_tmp.DefaultIfEmpty()
                            join stopCT in DbContext.ChargeTags on t.StopTagId equals stopCT.TagId into ft
                            from stopCT in ft.DefaultIfEmpty()
                            where (t.StartTime >= dbStartDate &&
                                   t.StartTime <= dbStopDate &&
                                   (!t.StopTime.HasValue || t.StopTime < dbStopDate))
                            select new TransactionExtended
                            {
                                TransactionId = t.TransactionId,
                                Uid = t.Uid,
                                ChargePointId = t.ChargePointId,
                                ConnectorId = t.ConnectorId,
                                StartTagId = t.StartTagId,
                                StartTime = t.StartTime,
                                MeterStart = t.MeterStart,
                                StartResult = t.StartResult,
                                StopTagId = t.StopTagId,
                                StopTime = t.StopTime,
                                MeterStop = t.MeterStop,
                                StopReason = t.StopReason,
                                StartTagName = startCT.TagName,
                                StartTagParentId = startCT.ParentTagId,
                                StopTagName = stopCT.TagName,
                                StopTagParentId = stopCT.ParentTagId
                            });

            if (!User.IsInRole(Constants.AdminRoleName))
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var assignedIds = DbContext.UserChargePoints
                        .Where(ucp => ucp.UserId == userId)
                        .Select(ucp => ucp.ChargePointId);
                    
                    transactionsQuery = transactionsQuery.Where(t => assignedIds.Contains(t.ChargePointId));
                }
                else
                {
                    transactionsQuery = transactionsQuery.Where(t => false);
                }
            }

            var transactions = transactionsQuery
                            .AsNoTracking()
                            .ToList();

            if (!string.IsNullOrWhiteSpace(group))
            {
                group = group.Trim();
                Logger.LogInformation($"Filtering Charge Report (In-Memory) by Group: '{group}'");
                transactions = transactions
                    .Where(t => t.StartTagParentId == group)
                    .ToList();
            }

            // generate and return grouped data
            return new ChargeReportViewModel
            {
                StartDate = startDate.Value,
                StopDate = stopDate.Value,
                GroupFilter = group,
                AvailableGroups = availableGroups,
                Groups = transactions
                    .GroupBy(t => t.StartTagParentId)
                    .OrderBy(g => g.Key) // Order groups by name
                    .Select(g => new GroupReport
                    {
                        GroupName = g.Key,
                        Tags = g.GroupBy(t => (string.IsNullOrEmpty(t.StartTagName) ? t.StartTagId : t.StartTagName))
                                .OrderBy(tg => tg.Key) // Order tags by name
                                .Select(tg => new TagReport
                                {
                                    TagName = tg.Key,
                                    Transactions = tg.Select(t => new TransactionReport
                                    {
                                        TransactionId = t.TransactionId,
                                        ChargePointId = t.ChargePointId,
                                        ConnectorId = t.ConnectorId,
                                        StartTagId = string.IsNullOrEmpty(t.StartTagName) ? t.StartTagId : t.StartTagName,
                                        StartTime = t.StartTime,
                                        MeterStart = t.MeterStart,
                                        StartResult = t.StartResult,
                                        StopTagId = string.IsNullOrEmpty(t.StopTagName) ? t.StopTagId : t.StopTagName,
                                        StopTime = t.StopTime,
                                        MeterStop = t.MeterStop,
                                        StopReason = t.StopReason
                                    }).ToList()
                                }).ToList()
                    }).ToList()
            };
        }

        private TransactionListViewModel GetAllTransactions(DateTime? startDate, DateTime? stopDate)
        {
            startDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddMonths(-1); // Default to first day of previous month
            stopDate ??= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1).AddDays(-1); // Default to last day of previous month

            // Restrict DateTime to date
            startDate = startDate.Value.Date;
            stopDate = stopDate.Value.Date;

            // Timestamps in DB are UTC
            DateTime dbStartDate = startDate.Value.ToUniversalTime();
            // Stop date => use next day and compare with "<" (no clock times needed)
            DateTime dbStopDate = stopDate.Value.AddDays(1).ToUniversalTime();

            var tlvm = new TransactionListViewModel
            {
                ConnectorStatuses = new List<ConnectorStatus>(),
                Transactions = new List<TransactionExtended>()
            };

            Logger.LogTrace("ChargeReport: Loading charge points and connectors...");
            var connectorQuery = DbContext.ConnectorStatuses.Include(cs => cs.ChargePoint).AsQueryable();
            var txQueryData = (from t in DbContext.Transactions
                                 join startCT in DbContext.ChargeTags on t.StartTagId equals startCT.TagId into ft_tmp
                                 from startCT in ft_tmp.DefaultIfEmpty()
                                 join stopCT in DbContext.ChargeTags on t.StopTagId equals stopCT.TagId into ft
                                 from stopCT in ft.DefaultIfEmpty()
                                 where (t.StartTime >= dbStartDate && 
                                        t.StartTime <= dbStopDate && 
                                        (!t.StopTime.HasValue || t.StopTime < dbStopDate))
                                 select new TransactionExtended
                                 {
                                     TransactionId = t.TransactionId,
                                     Uid = t.Uid,
                                     ChargePointId = t.ChargePointId,
                                     ConnectorId = t.ConnectorId,
                                     StartTagId = t.StartTagId,
                                     StartTime = t.StartTime,
                                     MeterStart = t.MeterStart,
                                     StartResult = t.StartResult,
                                     StopTagId = t.StopTagId,
                                     StopTime = t.StopTime,
                                     MeterStop = t.MeterStop,
                                     StopReason = t.StopReason,
                                     StartTagName = startCT.TagName,
                                     StartTagParentId = startCT.ParentTagId,
                                     StopTagName = stopCT.TagName,
                                     StopTagParentId = stopCT.ParentTagId
                                 });

            if (!User.IsInRole(Constants.AdminRoleName))
            {
                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                if (int.TryParse(userIdStr, out int userId))
                {
                    var assignedIds = DbContext.UserChargePoints
                        .Where(ucp => ucp.UserId == userId)
                        .Select(ucp => ucp.ChargePointId);
                    
                    connectorQuery = connectorQuery.Where(cs => assignedIds.Contains(cs.ChargePointId));
                    txQueryData = txQueryData.Where(t => assignedIds.Contains(t.ChargePointId));
                }
                else
                {
                    connectorQuery = connectorQuery.Where(cs => false);
                    txQueryData = txQueryData.Where(t => false);
                }
            }

            tlvm.ConnectorStatuses = connectorQuery.ToList();

            Logger.LogTrace("ChargeReport: Loading transactions...");
            tlvm.Transactions = txQueryData.AsNoTracking().ToList();

            return tlvm;
        }
    }
}