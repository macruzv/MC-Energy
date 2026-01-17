/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * Copyright (C) 2020-2025 dallmann consulting GmbH.
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
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class HomeController : BaseController
    {
        [Authorize]
        public IActionResult ChargePoint(string Id, ChargePointViewModel cpvm)
        {
            try
            {
                // Removed hardcoded Admin check. PermissionFilter handles general access.
                // However, we still need to filter the data if not admin.

                // Defensive fallbacks for localization
                string datePattern = System.Globalization.CultureInfo.CurrentCulture.DateTimeFormat.ShortDatePattern;
                if (string.IsNullOrEmpty(datePattern)) datePattern = "yyyy-MM-dd";
                ViewBag.DatePattern = datePattern;

                string language = System.Globalization.CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
                if (string.IsNullOrEmpty(language)) language = "en";
                ViewBag.Language = language;

                var cpQuery = DbContext.ChargePoints.AsQueryable();

                if (User != null && !User.IsInRole(Constants.AdminRoleName))
                {
                    var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    if (int.TryParse(userIdStr, out int userId))
                    {
                        var assignedIds = DbContext.UserChargePoints
                            .Where(ucp => ucp.UserId == userId)
                            .Select(ucp => ucp.ChargePointId);
                        
                        cpQuery = cpQuery.Where(cp => assignedIds.Contains(cp.ChargePointId));
                    }
                    else
                    {
                        cpQuery = cpQuery.Where(cp => false);
                    }
                }

                if (string.IsNullOrEmpty(Id))
                {
                    Logger.LogWarning("ChargePoint: Id is null or empty, redirecting to list");
                    cpvm = new ChargePointViewModel { ChargePoints = cpQuery.OrderBy(x => x.Name).ToList() };
                    return View("ChargePointList", cpvm);
                }

                cpvm.CurrentId = Id;

                List<ChargePoint> dbChargePoints = cpQuery
                    .Include(x => x.UserChargePoints)
                    .OrderBy(x => x.Name)
                    .ToList<ChargePoint>();
                Logger.LogInformation("ChargePoint: Found {0} chargepoints", dbChargePoints.Count);

                ChargePoint currentChargePoint = null;
                if (!string.IsNullOrEmpty(Id) && Id != "@")
                {
                    foreach (ChargePoint cp in dbChargePoints)
                    {
                        if (cp.ChargePointId.Equals(Id, StringComparison.InvariantCultureIgnoreCase))
                        {
                            currentChargePoint = cp;
                            Logger.LogTrace("ChargePoint: Current chargepoint: {0} / {1}", cp.ChargePointId, cp.Name);
                            break;
                        }
                    }
                }

                if (Request.Method == "POST")
                {
                    string errorMsg = null;

                    if (Id == "@")
                    {
                        Logger.LogTrace("ChargePoint: Creating new chargepoint...");

                        // Create new tag
                        if (string.IsNullOrWhiteSpace(cpvm.ChargePointId))
                        {
                            errorMsg = _localizer["ChargePointIdRequired"].Value;
                            Logger.LogInformation("ChargePoint: New => no chargepoint ID entered");
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            // check if duplicate
                            foreach (ChargePoint cp in dbChargePoints)
                            {
                                if (cp.ChargePointId.Equals(cpvm.ChargePointId, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    // id already exists
                                    errorMsg = _localizer["ChargePointIdExists"].Value;
                                    Logger.LogInformation("ChargePoint: New => chargepoint ID already exists: {0}", cpvm.ChargePointId);
                                    break;
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(errorMsg))
                        {
                            // Save tag in DB
                            ChargePoint newChargePoint = new ChargePoint();
                            newChargePoint.ChargePointId = cpvm.ChargePointId;
                            newChargePoint.Name = cpvm.Name;
                            newChargePoint.Comment = cpvm.Comment;
                            newChargePoint.Username = cpvm.Username;
                            newChargePoint.Password = cpvm.Password;
                            newChargePoint.ClientCertThumb = cpvm.ClientCertThumb;
                            newChargePoint.CreateDateTime = DateTime.UtcNow;
                            DbContext.ChargePoints.Add(newChargePoint);
                            DbContext.SaveChanges();
                            Logger.LogInformation("ChargePoint: New => charge point saved: {0} / {1}", cpvm.ChargePointId, cpvm.Name);
                        }
                        else
                        {
                            ViewBag.ErrorMsg = errorMsg;
                            return View("ChargePointDetail", cpvm);
                        }
                    }
                    else if (currentChargePoint != null && currentChargePoint.ChargePointId == Id)
                    {
                        if (Request.Form["action"] == "Delete")
                        {
                            // Delete existing tag
                            Logger.LogDebug("ChargeTag: Edit => Deleting tag {0} ...", currentChargePoint.ChargePointId);

                            using (var transaction = DbContext.Database.BeginTransaction())
                            {
                                try
                                {
                                    // Delete corresponding transactions
                                    var delTransactions = DbContext.Transactions.Where(t => t.ChargePointId == currentChargePoint.ChargePointId).ExecuteDelete();
                                    Logger.LogDebug("ChargeTag: Edit => Deleted {0} transactions", delTransactions);
                                    // Delete corresponding connectors
                                    var delConnectorStatuses = DbContext.ConnectorStatuses.Where(s => s.ChargePointId == currentChargePoint.ChargePointId).ExecuteDelete();
                                    Logger.LogDebug("ChargeTag: Edit => Deleted {0} connectors statuses", delConnectorStatuses);
                                    // And finally delete the chargeoint itself
                                    var delChargePoints = DbContext.ChargePoints.Where(c => c.ChargePointId == currentChargePoint.ChargePointId).ExecuteDelete();
                                    Logger.LogDebug("ChargeTag: Edit => Deleted {0} chargepoints", delChargePoints);

                                    if (delChargePoints == 1)
                                    {
                                        Logger.LogInformation("ChargeTag: Edit => Committing deletion of chargepoint '{0}'", currentChargePoint.ChargePointId);
                                        transaction.Commit();
                                    }
                                    else
                                    {
                                        Logger.LogWarning("ChargePoint: Deleting chargepoint '{0}' => no chargepoint with that ID deleted!?", currentChargePoint.ChargePointId);
                                        transaction.Rollback();
                                    }
                                }
                                catch (Exception exp)
                                {
                                    Logger.LogError(exp, "ChargePoint: Error deleting chargepoint '{0}' from database", currentChargePoint.ChargePointId);
                                    transaction.Rollback();
                                    throw;
                                }
                            }
                        }
                        else
                        {
                            // Save existing charge point
                            Logger.LogTrace("ChargePoint: Saving charge point '{0}'", Id);
                            currentChargePoint.Name = cpvm.Name;
                            currentChargePoint.Comment = cpvm.Comment;
                            currentChargePoint.Username = cpvm.Username;
                            currentChargePoint.Password = cpvm.Password;
                            currentChargePoint.ClientCertThumb = cpvm.ClientCertThumb;

                            // Update User Assignments
                            var currentAssignments = DbContext.UserChargePoints.Where(ucp => ucp.ChargePointId == Id);
                            DbContext.UserChargePoints.RemoveRange(currentAssignments);

                            if (cpvm.SelectedUserIds != null)
                            {
                                foreach (var userId in cpvm.SelectedUserIds)
                                {
                                    DbContext.UserChargePoints.Add(new UserChargePoint { UserId = userId, ChargePointId = Id });
                                }
                            }

                            DbContext.SaveChanges();
                            Logger.LogInformation("ChargePoint: Edit => chargepoint and assignments saved: {0} / {1}", cpvm.ChargePointId, cpvm.Name);
                        }
                    }

                    return RedirectToAction("ChargePoint", new { Id = "" });
                }
                else
                {
                    // Display charge point
                    cpvm.ChargePoints = dbChargePoints;
                    cpvm.CurrentId = Id;

                    if (currentChargePoint != null)
                    {
                        cpvm.ChargePointId = currentChargePoint.ChargePointId;
                        cpvm.Name = currentChargePoint.Name;
                        cpvm.Comment = currentChargePoint.Comment;
                        cpvm.Username = currentChargePoint.Username;
                        cpvm.Password = currentChargePoint.Password;
                        cpvm.ClientCertThumb = currentChargePoint.ClientCertThumb;
                        cpvm.CreateDateTime = currentChargePoint.CreateDateTime;

                        // Reciprocal Assignment: Load users with access
                        cpvm.SelectedUserIds = DbContext.UserChargePoints
                            .Where(ucp => ucp.ChargePointId == Id)
                            .Select(ucp => ucp.UserId)
                            .ToArray();
                    }

                    // Load all users for the picker
                    cpvm.AvailableUsers = DbContext.Users
                        .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
                        .OrderBy(u => u.Username)
                        .ToList();

                    string viewName = (!string.IsNullOrEmpty(cpvm.ChargePointId) || Id == "@") ? "ChargePointDetail" : "ChargePointList";
                    return View(viewName, cpvm);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "ChargePoint: Error loading/editing chargepoint(s)");
                TempData["ErrMessage"] = exp.Message;
                return RedirectToAction("Error", new { Id = "" });
            }
        }
    }
}
