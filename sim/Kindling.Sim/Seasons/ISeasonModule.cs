using System.Collections.Generic;
using Kindling.Sim.Model;

namespace Kindling.Sim.Seasons
{
    public interface ISeasonModule
    {
        string Id { get; }
        void OnMatchStart(MatchState m);
        void OnRecruitStart(PlayerState p);
        void OnCombatStart(object ctx);
        IEnumerable<object> ExtraOffers(PlayerState p);
        void ValidateAction(PlayerState p, RecruitAction a);
    }

    public sealed class SeasonNone : ISeasonModule
    {
        public string Id => "none";

        public void OnMatchStart(MatchState m) { }

        public void OnRecruitStart(PlayerState p) { }

        public void OnCombatStart(object ctx) { }

        public IEnumerable<object> ExtraOffers(PlayerState p)
        {
            yield break;
        }

        public void ValidateAction(PlayerState p, RecruitAction a) { }
    }
}
