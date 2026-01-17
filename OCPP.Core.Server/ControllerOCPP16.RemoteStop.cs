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
        public void HandleRemoteStopTransaction(OCPPMessage msgIn, OCPPMessage msgOut)
        {
            Logger.LogInformation("RemoteStopTransaction answer: ChargePointId={0} / MsgType={1} / ErrCode={2}", ChargePointStatus.Id, msgIn.MessageType, msgIn.ErrorCode);

            try
            {
                RemoteStopTransactionResponse remoteStopResponse = DeserializeMessage<RemoteStopTransactionResponse>(msgIn);
                Logger.LogInformation("RemoteStopTransaction => Answer status: {0}", remoteStopResponse?.Status);
                WriteMessageLog(ChargePointStatus?.Id, null, msgOut.Action, remoteStopResponse?.Status.ToString(), msgIn.ErrorCode);

                if (msgOut.TaskCompletionSource != null)
                {
                    // Set API response as TaskCompletion-result
                    string apiResult = "{\"status\": " + JsonConvert.ToString(remoteStopResponse.Status.ToString()) + "}";
                    Logger.LogTrace("HandleRemoteStopTransaction => API response: {0}", apiResult);

                    msgOut.TaskCompletionSource.SetResult(apiResult);
                }
            }
            catch (Exception exp)
            {
                Logger.LogError(exp, "{ControllerName} => HandleRemoteStopTransaction => Exception: {0}", GetType().Name, exp.Message);
                if (msgOut.TaskCompletionSource != null)
                {
                    msgOut.TaskCompletionSource.SetResult("{\"status\": \"Rejected\", \"error\": \"Internal server error processing response\"}");
                }
            }
        }
    }
}
