using DreamGate.Battlegrounds.Services.Backend;
using UnityEngine;

namespace DreamGate.Battlegrounds.Services
{
    public static class MatchmakingServiceFactory
    {
        public static IMatchmakingService Create(MonoBehaviour host)
        {
            var settings = BackendSettings.Load();
            if (settings != null && settings.IsConfigured && DreamGateServices.CloudClient != null && DreamGateServices.Profile != null)
            {
                return new BackendMatchmakingService(settings, DreamGateServices.CloudClient, DreamGateServices.Profile);
            }

            return new LocalMatchmakingService(host);
        }
    }
}