using System;

namespace DreamGate.Battlegrounds.Services
{
    public interface IMatchmakingService
    {
        bool IsSearching { get; }
        event Action<int, int> QueueUpdated;
        event Action<MatchmakingResult> MatchFound;
        event Action<string> QueueFailed;

        void StartQueue();
        void CancelQueue();
    }
}