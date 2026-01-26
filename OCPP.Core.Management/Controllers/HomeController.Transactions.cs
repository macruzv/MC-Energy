/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2021 dallmann consulting GmbH.
 * All Rights Reserved.
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <https://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult Transactions(string Id, string ConnectorId)
        {
            Logger.LogTrace("Transactions: Loading charge point transactions...");

            int currentConnectorId = -1;
            int.TryParse(ConnectorId, out currentConnectorId);

            TransactionListViewModel tlvm = new TransactionListViewModel();
            tlvm.CurrentChargePointId = Id;
            tlvm.CurrentConnectorId = currentConnectorId;
            tlvm.ConnectorStatuses = new List<ConnectorStatus>();
            tlvm.Transactions = new List<TransactionExtended>();

            try
            {
                string ts = Request.Query["t"];
                int days = 30;
                if (ts == "2")
                {
                    // 90 days
                    days = 90;
                    tlvm.Timespan = 2;
                }
                else if (ts == "3")
                {
                    // 365 days
                    days = 365;
                    tlvm.Timespan = 3;
                }
                else
                {
                    // 30 days
                    days = 30;
                    tlvm.Timespan = 1;
                }

                Logger.LogTrace("Transactions: Loading charge points...");
                var cpQuery = DbContext.ChargePoints.AsQueryable();
                var connQuery = DbContext.ConnectorStatuses.AsQueryable();

                if (!User.IsInRole(Constants.AdminRoleName))
                {
                    var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var assignedIds = DbContext.UserChargePoints
                            .Where(ucp => ucp.UserId == userId)
                            .Select(ucp => ucp.ChargePointId);
                        
                        cpQuery = cpQuery.Where(cp => assignedIds.Contains(cp.ChargePointId));
                        connQuery = connQuery.Where(cs => assignedIds.Contains(cs.ChargePointId));
                    }
                    else
                    {
                        cpQuery = cpQuery.Where(cp => false);
                        connQuery = connQuery.Where(cs => false);
                    }
                }

                tlvm.ChargePoints = cpQuery.ToList();
                tlvm.ConnectorStatuses = connQuery.ToList();

                // Count connectors for every charge point (=> naming scheme)
                Dictionary<string, int> dictConnectorCount = new Dictionary<string, int>();
                foreach (ConnectorStatus cs in tlvm.ConnectorStatuses)
                {
                    if (dictConnectorCount.ContainsKey(cs.ChargePointId))
                    {
                        // > 1 connector
                        dictConnectorCount[cs.ChargePointId] = dictConnectorCount[cs.ChargePointId] + 1;
                    }
                    else
                    {
                        // first connector
                        dictConnectorCount.Add(cs.ChargePointId, 1);
                    }
                }

                // Allow loading all transactions if no specific CP is selected
                // if (!string.IsNullOrEmpty(tlvm.CurrentChargePointId)) // Old check removed
                {
                    Logger.LogTrace("Transactions: Loading charge point transactions...");
                    
                    var txQuery = from t in DbContext.Transactions
                                join startCT in DbContext.ChargeTags on t.StartTagId equals startCT.TagId into ft_tmp
                                from startCT in ft_tmp.DefaultIfEmpty()
                                join stopCT in DbContext.ChargeTags on t.StopTagId equals stopCT.TagId into ft
                                from stopCT in ft.DefaultIfEmpty()
                                where (string.IsNullOrEmpty(tlvm.CurrentChargePointId) || t.ChargePointId == tlvm.CurrentChargePointId) &&
                                      (tlvm.CurrentConnectorId <= 0 || t.ConnectorId == tlvm.CurrentConnectorId) &&
                                      t.StartTime >= DateTime.UtcNow.AddDays(-1 * days)
                                select new { t, startCT, stopCT };

                    if (!User.IsInRole(Constants.AdminRoleName))
                    {
                        var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                        if (int.TryParse(userIdStr, out int userId))
                        {
                            var assignedIds = DbContext.UserChargePoints
                                .Where(ucp => ucp.UserId == userId)
                                .Select(ucp => ucp.ChargePointId);
                            
                            txQuery = txQuery.Where(x => assignedIds.Contains(x.t.ChargePointId));
                        }
                        else
                        {
                            txQuery = txQuery.Where(x => false);
                        }
                    }

                    tlvm.Transactions = txQuery.Select(x => new TransactionExtended
                                {
                                    TransactionId = x.t.TransactionId,
                                    Uid = x.t.Uid,
                                    ChargePointId = x.t.ChargePointId,
                                    ConnectorId = x.t.ConnectorId,
                                    StartTagId = x.t.StartTagId,
                                    StartTime = x.t.StartTime,
                                    MeterStart = x.t.MeterStart,
                                    StartResult = x.t.StartResult,
                                    StopTagId = x.t.StopTagId,
                                    StopTime = x.t.StopTime,
                                    MeterStop = x.t.MeterStop,
                                    StopReason = x.t.StopReason,
                                    StartTagName = x.startCT.TagName,
                                    StartTagParentId = x.startCT.ParentTagId,
                                    StopTagName = x.stopCT.TagName,
                                    StopTagParentId = x.stopCT.ParentTagId,
                                    IsAcknowledged = x.t.IsAcknowledged
                                })
                                .OrderByDescending(t => t.TransactionId)
                                .AsNoTracking()
                                .ToList();
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Transactions: Error loading charge points from database");
            }
 
            return View(tlvm);
        }


    }
}
