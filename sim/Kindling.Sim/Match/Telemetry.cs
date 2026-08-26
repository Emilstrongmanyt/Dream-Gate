namespace Kindling.Sim.Match
{
    public static class Telemetry
    {
        public static int MatchActive;
        public static int MatchFinished;
        public static int RecruitReject;
        public static int GlimpseOverflow;
        public static int GrantEmbersTotal;
        public static int AbandonTotal;
        public static int CheckpointWrites;

        public static string Snapshot()
        {
            return "{\"match_active\":" + MatchActive
                + ",\"match_finished\":" + MatchFinished
                + ",\"recruit_action_reject\":" + RecruitReject
                + ",\"glimpse_overflow\":" + GlimpseOverflow
                + ",\"grant_embers_total\":" + GrantEmbersTotal
                + ",\"abandon_total\":" + AbandonTotal
                + ",\"checkpoint_writes\":" + CheckpointWrites + "}";
        }
    }
}
