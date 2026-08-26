namespace Kindling.Sim.Match
{
    public interface IMatchStore
    {
        void PutMatch(string matchId, string json);
        string GetMatch(string matchId);
        void PutAccount(string accountId, string json);
        string GetAccount(string accountId);
        void PutDevice(string deviceHash, string accountId);
        string GetDevice(string deviceHash);
    }
}
