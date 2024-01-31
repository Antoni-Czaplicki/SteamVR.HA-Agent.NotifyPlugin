using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamVR.NotifyPlugin.Notification
{
    class Response
    {
        public Response(string nonce = "", bool success = true, string errorCode = "", string errorMessage = "", string type = "result")
        {
            this.nonce = nonce;
            this.type = type;
            this.success = success;
            this.error.code = errorCode;
        }

        public string nonce;
        public string type;
        public bool success;
        public Error error = new Error();

        public class Error
        {
            public string code = "";
            public string message = "";
        }
    }

    class ResponseWithSessionId
    {
        public ResponseWithSessionId(string sessionID, Response response) {
            this.sessionID = sessionID;
            this.response = response;
        }

        public string sessionID;
        public Response response;
    }
}