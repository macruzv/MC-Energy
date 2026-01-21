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
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public async Task<IActionResult> Connector(string Id, string ConnectorId, ConnectorStatusViewModel csvm)
        {
            try
            {
                // Removed hardcoded role check. PermissionFilter handles general access.
                // Data filtering logic below handles limited charger access.

                ViewBag.DatePattern = CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
                ViewBag.Language = CultureInfo.CurrentCulture.TwoLetterISOLanguageName;

                Logger.LogTrace("Connector: Loading connectors...");
                var connectorQuery = DbContext.ConnectorStatuses
                    .Where(x => string.IsNullOrEmpty(Id) || x.ChargePointId == Id);

                if (!User.IsInRole(Constants.AdminRoleName))
                {
                    var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var assignedIds = DbContext.UserChargePoints
                            .Where(ucp => ucp.UserId == userId)
                            .Select(ucp => ucp.ChargePointId);
                        
                        connectorQuery = connectorQuery.Where(cs => assignedIds.Contains(cs.ChargePointId));
                    }
                    else
                    {
                        connectorQuery = connectorQuery.Where(cs => false);
                    }
                }

                List<ConnectorStatus> dbConnectorStatuses = connectorQuery.ToList();
                Logger.LogInformation("Connector: Found {0} connectors", dbConnectorStatuses.Count);

                ConnectorStatus currentConnectorStatus = null;
                if (!string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(ConnectorId))
                {
                    foreach (ConnectorStatus cs in dbConnectorStatuses)
                    {
                        if (cs.ChargePointId.Equals(Id, StringComparison.InvariantCultureIgnoreCase) &&
                            cs.ConnectorId.ToString().Equals(ConnectorId, StringComparison.InvariantCultureIgnoreCase))
                        {
                            currentConnectorStatus = cs;
                            Logger.LogTrace("Connector: Current connector: {0} / {1}", cs.ChargePointId, cs.ConnectorId);
                            break;
                        }
                    }
                }

                if (Request.Method == "POST")
                {
                    if (currentConnectorStatus.ChargePointId == Id)
                    {
                        // Save connector
                        currentConnectorStatus.ConnectorName = csvm.ConnectorName;
                        DbContext.SaveChanges();
                        Logger.LogInformation("Connector: Edit => Connector saved: {0} / {1} => '{2}'", csvm.ChargePointId, csvm.ConnectorId, csvm.ConnectorName);
                    }

                    return RedirectToAction("Connector", new { Id = "" });
                }
                else
                {
                    // Get current status from server API if filtering by ID (Visual Mode)
                    ChargePointStatus onlineStatus = null;
                    if (!string.IsNullOrEmpty(Id))
                    {
                        string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                        if (!string.IsNullOrEmpty(serverApiUrl))
                        {
                            try
                            {
                                using (var httpClient = new HttpClient())
                                {
                                    if (!serverApiUrl.EndsWith('/')) serverApiUrl += "/";
                                    Uri uri = new Uri(new Uri(serverApiUrl), "Status");
                                    httpClient.Timeout = new TimeSpan(0, 0, 3);
                                    string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
                                    if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                                    {
                                        httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                                    }
                                    var response = await httpClient.GetAsync(uri);
                                    if (response.StatusCode == HttpStatusCode.OK)
                                    {
                                        var jsonData = await response.Content.ReadAsStringAsync();
                                        var list = JsonConvert.DeserializeObject<ChargePointStatus[]>(jsonData);
                                        onlineStatus = list?.FirstOrDefault(x => x.Id == Id);
                                    }
                                }
                            }
                            catch { /* ignore network errors */ }
                        }
                    }
                    ViewBag.OnlineStatus = onlineStatus;

                    // List all charge tags
                    csvm = new ConnectorStatusViewModel();
                    csvm.ConnectorStatuses = dbConnectorStatuses;

                    if (currentConnectorStatus != null)
                    {
                        csvm.ChargePointId = currentConnectorStatus.ChargePointId;
                        csvm.ConnectorId = currentConnectorStatus.ConnectorId;
                        csvm.ConnectorName = currentConnectorStatus.ConnectorName;
                        csvm.LastStatus = currentConnectorStatus.LastStatus;
                        csvm.LastStatusTime = currentConnectorStatus.LastStatusTime;
                        csvm.LastMeter = currentConnectorStatus.LastMeter;
                        csvm.LastMeterTime = currentConnectorStatus.LastMeterTime;
                    }

                    string viewName = (currentConnectorStatus != null) ? "ConnectorDetail" : "ConnectorList";
                    return View(viewName, csvm);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Connector: Error loading connectors from database");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }
        [Authorize]
        public async Task<IActionResult> Control(string Id, int ConnectorId)
        {
            try
            {
                // Removed hardcoded role check. PermissionFilter handles general access.
                // If they can see the CP, they can likely control it (Data filtering in Control page handles CP access).

                if (ConnectorId <= 0)
                {
                    // If no connector specified, try to find the first one for this station
                    var firstConn = DbContext.ConnectorStatuses.FirstOrDefault(x => x.ChargePointId == Id);
                    if (firstConn != null) ConnectorId = firstConn.ConnectorId;
                }

                // Get ChargePoint
                var cp = DbContext.ChargePoints.FirstOrDefault(x => x.ChargePointId == Id);
                if (cp == null) return NotFound();

                // Get current status from server API
                ChargePointStatus onlineStatus = null;
                string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                if (!string.IsNullOrEmpty(serverApiUrl))
                {
                    try
                    {
                        using (var httpClient = new HttpClient())
                        {
                            if (!serverApiUrl.EndsWith('/')) serverApiUrl += "/";
                            Uri uri = new Uri(new Uri(serverApiUrl), "Status");
                            httpClient.Timeout = new TimeSpan(0, 0, 3);
                            string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
                            if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                            {
                                httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                            }
                            var response = await httpClient.GetAsync(uri);
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                var jsonData = await response.Content.ReadAsStringAsync();
                                var list = JsonConvert.DeserializeObject<ChargePointStatus[]>(jsonData);
                                onlineStatus = list?.FirstOrDefault(x => x.Id == Id);
                            }
                        }
                    }
                    catch { /* ignore network errors, show as offline */ }
                }

                // Get Connector Info
                var dbConn = DbContext.ConnectorStatuses.FirstOrDefault(x => x.ChargePointId == Id && x.ConnectorId == ConnectorId);
                
                // Get Active Transaction
                var activeTx = DbContext.Transactions
                    .Where(t => t.ChargePointId == Id && t.ConnectorId == ConnectorId && t.StopTime == null)
                    .OrderByDescending(t => t.TransactionId)
                    .FirstOrDefault();

                // Logic Refinement: If charger reports Available but DB has an "active" transaction, 
                // it's a stale/ghost transaction. We should ignore it for UI purposes.
                if (onlineStatus != null && onlineStatus.OnlineConnectors != null && 
                    onlineStatus.OnlineConnectors.ContainsKey(ConnectorId))
                {
                    if (onlineStatus.OnlineConnectors.TryGetValue(ConnectorId, out var ocs) && ocs != null)
                    {
                        string realStatus = ocs.Status.ToString();
                        string ocppStatus = ocs.OcPPStatus ?? string.Empty;
                        
                        // If the charger is NOT currently Charging/Suspended but we have a transaction,
                        // and it's not in Preparing either, it might be a ghost session from a previous failure.
                        // IMPORTANT: We only clear activeTx if we are CERTAIN it's not a real charge.
                        // If ocppStatus is available, we use it. Otherwise we fall back to general Occupied.
                        bool isActuallyCharging = (ocppStatus == "Charging" || ocppStatus == "SuspendedEVSE" || ocppStatus == "SuspendedEV");
                        
                        if (activeTx != null && !isActuallyCharging)
                        {
                            // If status is Available, it's definitely a ghost.
                            // If it's Occupied/Preparing but NOT "Charging" according to raw OCPP, 
                            // and it's been some time, it might be a ghost (e.g. cable plugged but no session).
                            if (realStatus == "Available" || realStatus == "Unavailable" || realStatus == "Faulted")
                            {
                                activeTx = null;
                            }
                            else if (realStatus == "Occupied" || realStatus == "Preparing")
                            {
                                // If it's Occupied but NOT Charging according to raw OCPP, it might be just "Connected" 
                                // or it might be a manual start that hasn't reached "Charging" status yet.
                                // We keep the transaction if:
                                // 1. It started less than 1 minute ago (starting up).
                                // 2. Or there is actual power flow (ChargeRateKW > 0).
                                
                                bool hasPowerFlow = (ocs.ChargeRateKW > 0);
                                bool isVeryRecent = (activeTx.StartTime > DateTime.UtcNow.AddMinutes(-1));

                                if (!hasPowerFlow && !isVeryRecent)
                                {
                                    activeTx = null;
                                }
                            }
                        }
                    }
                }

                ViewBag.ChargePoint = cp;
                ViewBag.Connector = dbConn;
                ViewBag.ActiveTransaction = activeTx;
                
                string activeCustomerName = null;
                if (activeTx != null && !string.IsNullOrEmpty(activeTx.CustomerIdentifier))
                {
                    var customer = DbContext.Customers.FirstOrDefault(c => c.Identifier == activeTx.CustomerIdentifier);
                    activeCustomerName = customer?.Name;
                }
                ViewBag.ActiveCustomerName = activeCustomerName;

                ViewBag.OnlineStatus = onlineStatus;

                // Load valid Tags for manual selection
                ViewBag.Tags = await DbContext.ChargeTags
                    .Include(t => t.Customer)
                    .Where(t => t.Blocked != true)
                    .OrderBy(t => t.TagId)
                    .ToListAsync();

                return View();
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "Control: Error loading control page");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error");
            }
        }
        [Authorize]
        public async Task<IActionResult> QuickStart()
        {
            try
            {
                // Removed hardcoded role check. PermissionFilter handles general access.
                // Data filtering below restricts CP selection.

                // Get current status from server API
                Dictionary<string, ChargePointStatus> dictOnlineStatus = new Dictionary<string, ChargePointStatus>();
                string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                if (!string.IsNullOrEmpty(serverApiUrl))
                {
                    try
                    {
                        using (var httpClient = new HttpClient())
                        {
                            if (!serverApiUrl.EndsWith('/')) serverApiUrl += "/";
                            Uri uri = new Uri(new Uri(serverApiUrl), "Status");
                            httpClient.Timeout = new TimeSpan(0, 0, 3);
                            string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
                            if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                            {
                                httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                            }
                            var response = await httpClient.GetAsync(uri);
                            if (response.StatusCode == HttpStatusCode.OK)
                            {
                                var jsonData = await response.Content.ReadAsStringAsync();
                                var list = JsonConvert.DeserializeObject<ChargePointStatus[]>(jsonData);
                                if (list != null)
                                {
                                    foreach (var cps in list)
                                    {
                                        dictOnlineStatus[cps.Id] = cps;
                                    }
                                }
                            }
                        }
                    }
                    catch { /* ignore network errors */ }
                }
                ViewBag.OnlineStatus = dictOnlineStatus;

                ChargePointViewModel model = new ChargePointViewModel();
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

                model.ChargePoints = await cpQuery
                    .OrderBy(x => x.Name)
                    .ToListAsync();

                ViewBag.AllConnectors = await connQuery
                    .ToListAsync();

                // Load customers with their charge tags (including expiry info)
                var customersWithTags = await DbContext.Customers
                    .Include(c => c.ChargeTags)
                    .OrderBy(c => c.Name)
                    .ToListAsync();

                ViewBag.Customers = customersWithTags;

                return View(model);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "QuickStart: Error loading charge points");
                return RedirectToAction("Error", new { Id = "" });
            }
        }
        [Authorize]
        public IActionResult Ticket(int id)
        {
            var tx = DbContext.Transactions.FirstOrDefault(t => t.TransactionId == id);
            if (tx == null) return NotFound();

            // Buscar el nombre del cliente asociado al Tag
            string customerName = null;
            if (!string.IsNullOrEmpty(tx.StartTagId))
            {
                var tag = DbContext.ChargeTags
                    .Include(t => t.Customer)
                    .FirstOrDefault(t => t.TagId == tx.StartTagId);
                
                customerName = tag?.Customer?.Name;
            }

            // Si hay un cliente manual específico en la transacción, este tiene prioridad
            if (!string.IsNullOrEmpty(tx.CustomerIdentifier))
            {
                var manualCustomer = DbContext.Customers.FirstOrDefault(c => c.Identifier == tx.CustomerIdentifier);
                customerName = manualCustomer?.Name ?? tx.CustomerIdentifier;
            }

            ViewBag.CustomerName = customerName;

            // Cargar información de usuarios (operador y cobrador)
            string operatorName = null;
            string collectorName = null;

            if (tx.OperatorUserId.HasValue)
            {
                var operatorUser = DbContext.Users.FirstOrDefault(u => u.UserId == tx.OperatorUserId.Value);
                operatorName = !string.IsNullOrEmpty(operatorUser?.Name) ? operatorUser.Name : operatorUser?.Username;
            }

            if (tx.CollectorUserId.HasValue)
            {
                var collectorUser = DbContext.Users.FirstOrDefault(u => u.UserId == tx.CollectorUserId.Value);
                collectorName = !string.IsNullOrEmpty(collectorUser?.Name) ? collectorUser.Name : collectorUser?.Username;
            }

            ViewBag.OperatorName = operatorName;
            ViewBag.CollectorName = collectorName;

            var priceSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "PricePerKWh");
            ViewBag.PricePerKWh = priceSetting?.Value ?? "0.00";

            var nameSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyName");
            ViewBag.CompanyName = nameSetting?.Value ?? "ENERGY CORE MC";

            var addressSetting = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "CompanyAddress");
            ViewBag.CompanyAddress = addressSetting?.Value ?? "Estación de Carga Eléctrica";

            ViewBag.PrinterDPI = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_DPI")?.Value ?? "150";
            ViewBag.PrinterWidth = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Printer_Width")?.Value ?? "56";

            // Billing Settings
            ViewBag.BillingMode = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Billing_Mode")?.Value ?? "Energy";
            ViewBag.PricingType = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Pricing_Type")?.Value ?? "Fixed";
            ViewBag.PricingSchedules = DbContext.SystemSettings.FirstOrDefault(s => s.SettingId == "Pricing_Schedules")?.Value ?? "[]";

            return View(tx);
        }
    }
}
