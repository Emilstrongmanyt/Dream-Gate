using System.Text;
using UnityEngine.Networking;

namespace DreamGate.Battlegrounds.Services.Backend
{
    internal static class WebRequestHelper
    {
        public static string ReadResponseText(UnityWebRequest request)
        {
            var handler = request?.downloadHandler;
            if (handler == null)
            {
                return string.Empty;
            }

            var data = handler.data;
            if (data != null && data.Length > 0)
            {
                return Encoding.UTF8.GetString(data);
            }

            return handler.text ?? string.Empty;
        }

        public static void ConfigureJsonPost(UnityWebRequest request, byte[] body, bool disposeHandlers = true)
        {
            request.uploadHandler = new UploadHandlerRaw(body)
            {
                contentType = "application/json"
            };
            request.downloadHandler = new DownloadHandlerBuffer();
            request.disposeUploadHandlerOnDispose = disposeHandlers;
            request.disposeDownloadHandlerOnDispose = disposeHandlers;
            request.chunkedTransfer = false;
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            request.SetRequestHeader("Accept-Encoding", "identity");
        }

        public static string DescribeTransportError(UnityWebRequest request, string responseBody)
        {
            var parsed = ApiJson.TryGetString(responseBody, "msg")
                         ?? ApiJson.TryGetString(responseBody, "error_description")
                         ?? ApiJson.TryGetString(responseBody, "error")
                         ?? ApiJson.TryGetString(responseBody, "message");
            if (!string.IsNullOrWhiteSpace(parsed))
            {
                return parsed;
            }

            if (!string.IsNullOrWhiteSpace(request?.error))
            {
                return request.error;
            }

            return $"Request failed (HTTP {request?.responseCode ?? 0}).";
        }
    }
}