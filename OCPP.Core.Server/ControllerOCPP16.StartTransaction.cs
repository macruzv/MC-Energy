﻿/*
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
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Microsoft.EntityFrameworkCore;
using OCPP.Core.Database;
using OCPP.Core.Server.Extensions.Interfaces;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public string HandleStartTransaction(OCPPMessage msgIn, OCPPMessage msgOut, OCPPMiddleware ocppMiddleware)
        {
            string errorCode = null;
            StartTransactionResponse startTransactionResponse = new StartTransactionResponse();

            int connectorId = -1;
            ChargeTag ct = null;
            bool denyConcurrentTx = Configuration.GetValue<bool>("DenyConcurrentTx", false);

            try
            {
                Logger.LogTrace("Processing startTransaction request...");
                StartTransactionRequest startTransactionRequest = DeserializeMessage<StartTransactionRequest>(msgIn);
                Logger.LogTrace("StartTransaction => Message deserialized");

                string idTag = CleanChargeTagId(startTransactionRequest.IdTag, Logger);
                if (string.IsNullOrWhiteSpace(idTag))
                {
                    idTag = "21222122";
                    Logger.LogInformation("StartTransaction => Empty tag detected, using default: {0}", idTag);
                }

                // Logic Refinement: Whitelist system tag for remote starts
                if (idTag == "21222122")
                {
                    startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                    startTransactionResponse.IdTagInfo.ParentIdTag = string.Empty;
                    startTransactionResponse.IdTagInfo.ExpiryDate = MaxExpiryDate;
                    Logger.LogInformation("StartTransaction => System tag '{0}' whitelisted.", idTag);
                }
                else
                {
                    ct = DbContext.ChargeTags.Include(t => t.Customer).FirstOrDefault(t => t.TagId == idTag);
                    connectorId = startTransactionRequest.ConnectorId;

                    startTransactionResponse.IdTagInfo.ParentIdTag = string.Empty;
                    startTransactionResponse.IdTagInfo.ExpiryDate = MaxExpiryDate;

                    bool? externalAuthResult = null;
                    try
                    {
                        externalAuthResult = ocppMiddleware.ProcessExternalAuthorizations(AuthAction.StartStransaction, idTag, ChargePointStatus.Id, connectorId, string.Empty, string.Empty);
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "{ControllerName} => StartTransaction => Exception from external authorization: {0}", GetType().Name, exp.Message);
                    }

                    if (externalAuthResult.HasValue)
                    {
                        if (externalAuthResult.Value)
                        {
                            startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;
                        }
                        else
                        {
                            startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                        }
                        Logger.LogInformation("StartTransaction => Extension auth. : Charge tag='{0}' => Status: {1}", idTag, startTransactionResponse.IdTagInfo.Status);
                    }
                    else
                    {
                        try
                        {
                            if (ct != null)
                            {
                                if (ct.ExpiryDate.HasValue) startTransactionResponse.IdTagInfo.ExpiryDate = ct.ExpiryDate.Value;
                                startTransactionResponse.IdTagInfo.ParentIdTag = ct.ParentTagId;
                                if (ct.Blocked.HasValue && ct.Blocked.Value)
                                {
                                    startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Blocked;
                                }
                                else if (ct.ExpiryDate.HasValue && ct.ExpiryDate.Value < DateTime.Now)
                                {
                                    startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Expired;

                                    // Auto-block expired tag
                                    ct.Blocked = true;
                                    DbContext.SaveChanges();
                                    Logger.LogInformation("StartTransaction => Tag '{0}' expired and was automatically blocked.", idTag);
                                }
                                else
                                {
                                    startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Accepted;

                                    if (denyConcurrentTx)
                                    {
                                        // Check that no open transaction with this idTag exists
                                        Transaction tx = DbContext.Transactions
                                            .Where(t => !t.StopTime.HasValue && t.StartTagId == ct.TagId)
                                            .OrderByDescending(t => t.TransactionId)
                                            .FirstOrDefault();

                                        if (tx != null)
                                        {
                                            startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.ConcurrentTx;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                            }

                            Logger.LogInformation("StartTransaction => Internal auth. : Charge tag='{0}' => Status: {1}", idTag, startTransactionResponse.IdTagInfo.Status);
                        }
                        catch (Exception exp)
                        {
                            Logger.LogError(exp, "{ControllerName} => StartTransaction => Exception reading charge tag ({0}): {1}", GetType().Name, idTag, exp.Message);
                            startTransactionResponse.IdTagInfo.Status = IdTagInfoStatus.Invalid;
                        }
                    }
                }

                if (connectorId > 0)
                {
                    // Update meter value in db connector status 
                    UpdateConnectorStatus(connectorId, ConnectorStatusEnum.Occupied.ToString(), startTransactionRequest.Timestamp, (double)startTransactionRequest.MeterStart / 1000, startTransactionRequest.Timestamp);
                }

                if (startTransactionResponse.IdTagInfo.Status == IdTagInfoStatus.Accepted)
                {
                    try
                    {
                        Transaction transaction = new Transaction();
                        transaction.ChargePointId = ChargePointStatus?.Id;
                        transaction.ConnectorId = startTransactionRequest.ConnectorId;
                        transaction.StartTagId = idTag;
                        transaction.StartTime = startTransactionRequest.Timestamp.UtcDateTime;
                        transaction.MeterStart = (double)startTransactionRequest.MeterStart / 1000; // Meter value here is always Wh
                        transaction.StartResult = startTransactionResponse.IdTagInfo.Status.ToString();

                        // Try to get pending customer info from side-channel (Manual input take precedence)
                        Logger.LogInformation("StartTransaction => Looking for pending customer data [CP={0}, CN={1}]", ChargePointStatus?.Id, startTransactionRequest.ConnectorId);
                        
                        // DEBUG: Inspect all keys
                        var allKeys = OCPPMiddleware.GetAllPendingCustomerKeys();
                        Logger.LogInformation("StartTransaction => Available Pending Keys: {0}", string.Join(", ", allKeys.Select(k => $"({k.Item1}, {k.Item2})")));

                        var customerData = OCPPMiddleware.GetPendingCustomerData(ChargePointStatus?.Id, startTransactionRequest.ConnectorId);
                        
                        // Relaxed Fallback: Check ANY connector for this ChargePoint
                        if (customerData == null)
                        {
                            Logger.LogInformation("StartTransaction => Data not found on specific connector {0}. Checking any connector for CP {1}...", startTransactionRequest.ConnectorId, ChargePointStatus?.Id);
                            
                            var allPending = OCPPMiddleware.GetAllPendingCustomerKeys();
                            foreach (var key in allPending)
                            {
                                if (key.Item1 == ChargePointStatus?.Id)
                                {
                                    Logger.LogInformation("StartTransaction => Found pending data on alternative connector: {0}", key.Item2);
                                    customerData = OCPPMiddleware.GetPendingCustomerData(key.Item1, key.Item2);
                                    break;
                                }
                            }
                        }

                        if (customerData != null)
                        {
                            transaction.CustomerIdentifier = customerData.Identifier;
                            transaction.CustomerPhone = customerData.Phone;
                            transaction.CustomerEmail = customerData.Email;
                            transaction.OperatorUserId = customerData.OperatorUserId;
                            Logger.LogInformation("StartTransaction => Enriched transaction with manual customer data: {0} / Operator: {1}", transaction.CustomerIdentifier, transaction.OperatorUserId);
                        }
                        else 
                        {
                             Logger.LogWarning("StartTransaction => No pending customer data found for {0}/{1}", ChargePointStatus?.Id, startTransactionRequest.ConnectorId);
                             
                             if (ct != null && ct.Customer != null)
                             {
                                 // Auto-link from RFID
                                 transaction.CustomerIdentifier = ct.Customer.Identifier;
                                 transaction.CustomerPhone = ct.Customer.Phone;
                                 transaction.CustomerEmail = ct.Customer.Email;
                                 Logger.LogInformation("StartTransaction => Auto-linked customer from RFID: {0}", ct.Customer.Name);
                             }
                        }

                        DbContext.Add<Transaction>(transaction);
                        DbContext.SaveChanges();

                        Logger.LogInformation("StartTransaction => Transaction {0} started at {1} kWh [CP={2}, CN={3}]", transaction.TransactionId, transaction.MeterStart, ChargePointStatus?.Id, transaction.ConnectorId);

                        // Return DB-ID as transaction ID
                        startTransactionResponse.TransactionId = transaction.TransactionId;
                    }
                    catch (Exception exp)
                    {
                        Logger.LogError(exp, "{ControllerName} => StartTransaction => Exception writing transaction: chargepoint={0} / tag={1}", GetType().Name, ChargePointStatus?.Id, idTag);
                        errorCode = ErrorCodes.InternalError;
                    }
                }

                msgOut.JsonPayload = JsonConvert.SerializeObject(startTransactionResponse);
                Logger.LogTrace("StartTransaction => Response serialized");
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "{ControllerName} => StartTransaction => Exception: {0}", GetType().Name, exp.Message);
                errorCode = ErrorCodes.FormationViolation;
            }

            WriteMessageLog(ChargePointStatus?.Id, connectorId, msgIn.Action, startTransactionResponse.IdTagInfo?.Status.ToString(), errorCode);
            return errorCode;
        }
    }
}
