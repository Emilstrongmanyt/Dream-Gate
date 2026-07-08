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

        public bool IsConfigured =>
            useCloudBackend &&
            !string.IsNullOrWhiteSpace(supabaseUrl) &&
            !string.IsNullOrWhiteSpace(supabaseAnonKey);

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