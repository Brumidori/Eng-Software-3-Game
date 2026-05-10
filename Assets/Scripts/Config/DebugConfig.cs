public static class DebugConfig
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool SkipLogin = false;
    public static string SkipLoginTargetScene = "DeckAdminMenu";
#endif
}
