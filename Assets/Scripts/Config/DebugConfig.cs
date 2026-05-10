public static class DebugConfig
{
#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public static bool SkipLogin = false;
    public static string SkipLoginTargetScene = "DeckAdminMenu";

    // Credenciais de uma conta admin real para testes sem passar pelo login
    public static string DebugEmail    = "teste@example.com";
    public static string DebugPassword = "senha123";
#endif
}
