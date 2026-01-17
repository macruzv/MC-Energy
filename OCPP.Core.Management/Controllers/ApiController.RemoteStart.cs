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
using System.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;
using OCPP.Core.Database;
using OCPP.Core.Management.Models;

namespace OCPP.Core.Management.Controllers
{
    public partial class ApiController : BaseController
    {
        [Authorize]
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        [HttpGet("RemoteStart/{Id}")]
        public async Task<IActionResult> RemoteStart(string Id, int cId, string tag, string customerId = null, string customerPhone = null, string customerEmail = null)
        {
            if (User != null && !User.IsInRole(Constants.AdminRoleName) && !User.IsInRole(Constants.OperatorRoleName))
            {
                Logger.LogWarning("RemoteStart: Request by unauthorized user: {0}", User?.Identity?.Name);
                return StatusCode((int)HttpStatusCode.Unauthorized);
            }

            int httpStatuscode = (int)HttpStatusCode.OK;
            string resultContent = string.Empty;

            Logger.LogTrace("RemoteStart: Request to start chargepoint '{0}' / Connector '{1}' / Tag '{2}' / Customer: {3}", Id, cId, tag, customerId);
            if (!string.IsNullOrEmpty(Id))
            {
                try
                {
                    string serverApiUrl = base.Config.GetValue<string>("ServerApiUrl");
                    string apiKeyConfig = base.Config.GetValue<string>("ApiKey");
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
                                // The server expecting: RemoteStart/{chargePointId}/{connectorId}/{idTag}
                                string remoteStartPath = $"RemoteStart/{Uri.EscapeDataString(Id)}/{cId}/{Uri.EscapeDataString(tag ?? "21222122")}";
                                
                                // Append customer data if available
                                List<string> queryParams = new List<string>();
                                if (!string.IsNullOrEmpty(customerId)) queryParams.Add($"cid={Uri.EscapeDataString(customerId)}");
                                if (!string.IsNullOrEmpty(customerPhone)) queryParams.Add($"tel={Uri.EscapeDataString(customerPhone)}");
                                if (!string.IsNullOrEmpty(customerEmail)) queryParams.Add($"eml={Uri.EscapeDataString(customerEmail)}");
                                
                                if (queryParams.Count > 0)
                                {
                                    remoteStartPath += "?" + string.Join("&", queryParams);
                                }

                                uri = new Uri(uri, remoteStartPath);
                                httpClient.Timeout = new TimeSpan(0, 0, 10); 

                                if (!string.IsNullOrWhiteSpace(apiKeyConfig))
                                {
                                    httpClient.DefaultRequestHeaders.Add("X-API-Key", apiKeyConfig);
                                }

                                // Validation: Check status before starting
                                try
                                {
                                    Uri statusUri = new Uri(new Uri(serverApiUrl), "Status");
                                    var statusResponse = await httpClient.GetAsync(statusUri);
                                    if (statusResponse.StatusCode == HttpStatusCode.OK)
                                    {
                                        var jsonData = await statusResponse.Content.ReadAsStringAsync();
                                        var list = JsonConvert.DeserializeObject<ChargePointStatus[]>(jsonData);
                                        var onlineStatus = list?.FirstOrDefault(x => x.Id == Id);
                                        
                                        bool statusOk = false;
                                        if (onlineStatus != null && onlineStatus.OnlineConnectors != null && onlineStatus.OnlineConnectors.ContainsKey(cId))
                                        {
                                            string s = onlineStatus.OnlineConnectors[cId].Status.ToString();
                                            if (s == "Preparing" || s == "Occupied") statusOk = true;
                                        }

                                        if (!statusOk)
                                        {
                                            return StatusCode((int)HttpStatusCode.OK, "Error: El vehículo no está conectado. Por favor, conecte la manguera GBT al vehículo antes de iniciar.");
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    Logger.LogWarning(ex, "RemoteStart: Pre-validation status check failed");
                                    // Proceed anyway or block? Given user's request, we should probably at least log it. 
                                    // Skipping block on status check error to not break functionality if API is slow, 
                                    // but the primary check is against the actual status returned.
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
                                            resultContent = "Comando de inicio aceptado por el cargador.";
                                        }
                                        else
                                        {
                                            resultContent = $"El cargador rechazó el comando de inicio (Estado: {status}). Verifica que esté conectado y listo.";
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
                                    resultContent = "Error en la comunicación con el servidor OCPP.";
                                }
                            }
                        }
                        catch (Exception exp)
                        {
                            Logger.LogError(exp, "RemoteStart: Error in API request => {0}", exp.Message);
                            resultContent = "Error de red al intentar iniciar el cargador.";
                        }
                    }
                }
                catch (Exception exp)
                {
                    Logger.LogError(exp, "RemoteStart: Error");
                    resultContent = "Error de configuración.";
                }
            }

            return StatusCode(httpStatuscode, resultContent);
        }
    }
}
