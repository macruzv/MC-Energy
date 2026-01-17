/*
 * OCPP.Core - https://github.com/dallmann-consulting/OCPP.Core
 * All Rights Reserved.
 */

using System;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OCPP.Core.Database;
using OCPP.Core.Server.Messages_OCPP16;

namespace OCPP.Core.Server
{
    public partial class ControllerOCPP16
    {
        public void HandleRemoteStartTransaction(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogInformation("RemoteStartTransaction answer: ChargePointId={0} / MsgType={1} / ErrCode={2}", ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

            try
            {
                RemoteStartTransactionResponse remoteStartResponse = DeserializeMessage<RemoteStartTransactionResponse>(msgIn);
                Logger.LogInformation("RemoteStartTransaction => Answer status: {0}", remoteStartResponse?.Status);
                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, remoteStartResponse?.Status.ToString(), msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // Set API response as TaskCompletion-result
                    string apiResult = "{\"status\": " + JsonConvert.ToString(remoteStartResponse.Status.ToString()) + "}";
                    Logger.LogTrace("HandleRemoteStartTransaction => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "{ControllerName} => HandleRemoteStartTransaction => Exception: {0}", GetType().Name, exp.Message);
                if (msgOut.TaskCompletionSource != null)
                {
                    msgOut.TaskCompletionSource.SetResult("{\"status\": \"Rejected\", \"error\": \"Internal server error processing response\"}");
                }
            }
        }
    }
}
