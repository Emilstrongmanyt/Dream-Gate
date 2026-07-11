using UnityEngine;

namespace DreamGate.Battlegrounds.Services.Backend
{
    [CreateAssetMenu(fileName = "BackendSettings", menuName = "Dream Gate/Backend Settings")]
    public class BackendSettings : ScriptableObject
    {
        private const string FallbackSupabaseUrl = "https://hekknzzbudmkwxtzxkdi.supabase.co";
        private const string FallbackAnonKey =
            "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Imhla2tuenpidWRta3d4dHp4a2RpIiwicm9sZSI6ImFub24iLCJpYXQiOjE3ODM1NDgxMTUsImV4cCI6MjA5OTEyNDExNX0.Ahxxv2De9RfeTwhJWARCbyv0HoUc7R1bx6Jv662oraI";

        [Header("Supabase")]
        public bool useCloudBackend = false;
        public string supabaseUrl = "";
        public string supabaseAnonKey = "";

        public string EffectiveSupabaseUrl =>
            string.IsNullOrWhiteSpace(supabaseUrl) ? FallbackSupabaseUrl : supabaseUrl.Trim().TrimEnd('/');

        public string EffectiveAnonKey =>
            string.IsNullOrWhiteSpace(supabaseAnonKey) ? FallbackAnonKey : supabaseAnonKey.Trim();

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

        public string ResolvedUgsSessionUrl => BuildFunctionUrl("ugs-session");

        public string ResolvedMatchServerUrl => matchServerWebSocketUrl?.Trim() ?? string.Empty;

        public bool HasMatchServer => !string.IsNullOrWhiteSpace(ResolvedMatchServerUrl);

        public bool IsConfigured =>
            useCloudBackend &&
            !string.IsNullOrWhiteSpace(EffectiveSupabaseUrl) &&
            !string.IsNullOrWhiteSpace(EffectiveAnonKey);

        private string BuildFunctionUrl(string functionName)
        {
            if (string.IsNullOrWhiteSpace(EffectiveSupabaseUrl))
            {
                return string.Empty;
            }

            return $"{EffectiveSupabaseUrl}/functions/v1/{functionName}";
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