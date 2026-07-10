namespace DreamGate.Battlegrounds.Services.Backend
{
    public static class OAuthConstants
    {
        public const string RedirectScheme = "com.solodreams.dreamgate";
        public const string RedirectPath = "auth/callback";
        public const string RedirectUrl = RedirectScheme + "://" + RedirectPath;
    }
}