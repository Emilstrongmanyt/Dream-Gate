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

            var text = handler.text;
            if (!string.IsNullOrEmpty(text))
            {
                return text;
            }

            var data = handler.data;
            if (data == null || data.Length == 0)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(data);
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
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
        }
    }
}