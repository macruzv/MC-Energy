/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * All Rights Reserved.
 */

using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;

namespace OCPP.Core.Management.Controllers
{
    public partial class ApiController : BaseController
    {
        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public async Task<IActionResult> RemoteStop(string Id, string tId, int cId = 0)
        {
            if (User != null && !User.IsInRole(Constants.AdminRoleName) && !User.IsInRole(Constants.OperatorRoleName))
            {
                Logger.LogWarning("RemoteStop: Request by unauthorized user: {0}", User?.Identity?.Name);
                return StatusCode((int)HttpStatusCode.Unauthorized);
            }

            int httpStatuscode = (int)HttpStatusCode.OK;
            string resultContent = string.Empty;

            Logger.LogTrace("RemoteStop: Request to stop chargepoint '{0}' / Connector '{1}' / Transaction '{2}'", Id, cId, tId);
            if (!string.IsNullOrEmpty(Id) && !string.IsNullOrEmpty(tId))
            {
                try
                {
                    string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                    string apiKeyConfig = base.Config.GetValue<string>("ApiKey");

                    // Track Collector (User who stopped the charge)
                    if (int.TryParse(tId, out int transactionId))
                    {
                        try 
                        {
                            var transaction = DbContext.Transactions.Find(transactionId);
                            if (transaction != null)
                            {
                                var userIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                                if (!string.IsNullOrEmpty(userIdStr) && int.TryParse(userIdStr, out int uid))
                                {
                                     transaction.CollectorUserId = uid;
                                     DbContext.SaveChanges();
                                     Logger.LogInformation("RemoteStop: Set CollectorUserId={0} for TransactionId={1}", uid, transactionId);
                                }
                            }
                        }
                        catch(Exception ex)
                        {
                            Logger.LogError(ex, "RemoteStop: Error updating CollectorUserId");
                        }
                    }

                    if (!string.IsNullOrEmpty(serverApiUrl))
                    {
                        try
                        {
                            using (var httpClient = new HttpClient())
                            {
                                if (!serverApiUrl.EndsWith('/'))
                                {
                                    serverApiUrl += "/";
                                }
                                Uri uri = new Uri(serverApiUrl);
                                uri = new Uri(uri, $"RemoteStop/{Uri.EscapeDataString(Id)}/{cId}/{Uri.EscapeDataString(tId)}");
                                httpClient.Timeout = new TimeSpan(0, 0, 10); 

                                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                                {
                                    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                                }

                                HttpResponseMessage response = await httpClient.GetAsync(uri);
                                if (response.StatusCode == HttpStatusCode.OK)
                                {
                                    string jsonResult = await response.Content.ReadAsStringAsync();
                                    if (!string.IsNullOrEmpty(jsonResult))
                                    {
                                        dynamic jsonObject = JsonConvert.DeserializeObject(jsonResult);
                                        string status = jsonObject.status ?? jsonObject.Status;
                                        if (string.Equals(status, "Accepted", StringComparison.OrdinalIgnoreCase))
                                        {
                                            resultContent = "Comando de parada aceptado por el cargador.";
                                        }
                                        else
                                        {
                                            resultContent = $"El cargador rechazó el comando de parada (Estado: {status}).";
                                        }
                                    }
                                    else
                                    {
                                        resultContent = "Error al procesar la respuesta del servidor (cuerpo vacío).";
                                    }
                                }
                                else if (response.StatusCode == HttpStatusCode.NotFound)
                                {
                                    resultContent = "Cargador offline o no encontrado.";
                                }
                                else
                                {
                                    resultContent = $"Error del servidor: {response.StatusCode}";
                                }
                            }
                        }
                        catch (Exception exp)
                        {
                            Logger.LogError(exp, "RemoteStop: Error in API request => {0}", exp.Message);
                            resultContent = $"Error de red: {exp.Message}";
                        }
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "RemoteStop: Error loading config");
                    resultContent = "Error de configuración (excepción).";
                }
            }

            return StatusCode(httpStatuscode, resultContent);
        }
    }
}
