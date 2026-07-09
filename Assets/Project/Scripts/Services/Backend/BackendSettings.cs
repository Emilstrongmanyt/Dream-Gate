using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    [CreateAssetMenu(fileName = "BackendSettings", menuName = "Dream Gate/Backend Settings")]
    public class BackendSettings : ScriptableObject
    {
        [Header("Supabase")]
        public bool useCloudBackend = false;
        public string supabaseUrl = "";
        public string supabaseAnonKey = "";

        [Header("Edge Functions")]
        public string matchmakingFunctionUrl = "";
        public string applyMatchResultFunctionUrl = "";

        [Header("Rated Match Server")]
        public string matchServerWebSocketUrl = "";

        public string ResolvedMatchmakingUrl =>
            !string.IsNullOrWhiteSpace(matchmakingFunctionUrl)
                ? matchmakingFunctionUrl
                : BuildFunctionUrl("matchmaking");

        public string ResolvedApplyMatchResultUrl =>
            !string.IsNullOrWhiteSpace(applyMatchResultFunctionUrl)
                ? applyMatchResultFunctionUrl
                : BuildFunctionUrl("apply-match-result");

        public string ResolvedMatchServerUrl => matchServerWebSocketUrl?.Trim() ?? string.Empty;

        public bool HasMatchServer => !string.IsNullOrWhiteSpace(ResolvedMatchServerUrl);

        public bool IsConfigured =>
            useCloudBackend &&
            !string.IsNullOrWhiteSpace(supabaseUrl) &&
            !string.IsNullOrWhiteSpace(supabaseAnonKey);

        private string BuildFunctionUrl(string functionName)
        {
            if (string.IsNullOrWhiteSpace(supabaseUrl))
            {
                return string.Empty;
            }

            return $"{supabaseUrl.TrimEnd('/')}/functions/v1/{functionName}";
        }

        private static BackendSettings cached;

        public static BackendSettings Load()
        {
            if (cached != null)
            {
                return cached;
            }

            cached = Resources.Load<BackendSettings>("BackendSettings");
            return cached;
        }
    }
}