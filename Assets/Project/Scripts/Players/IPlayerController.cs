namespace DreamGate.Battlegrounds.Players
{
    /// <summary>
    /// Abstraction for human, bot, and future networked player controllers.
    /// </summary>
    public interface IPlayerController
    {
        int PlayerId { get; }
        bool IsHuman { get; }
        void OnRecruitPhaseStarted(PlayerState state, int turn);
    }
}