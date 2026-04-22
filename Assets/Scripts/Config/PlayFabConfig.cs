using UnityEngine;

public static class PlayFabConfig
{
    private const string PersistentCustomIdKey = "PlayFab_TestCustomId";
    private const string DevelopmentTitleId = "15571";
    private const string FixedTestUserId = "teste_user_456";

    /// <summary>
    /// Define o ambiente de execução (Desenvolvimento, Staging, Produção)
    /// </summary>
    public enum Environment { Development, Staging, Production }

    public static Environment CurrentEnv = Environment.Development;

    /// <summary>
    /// Retorna o Title ID baseado no ambiente configurado
    /// </summary>
    public static string GetTitleId()
    {
        return CurrentEnv switch
        {
            Environment.Development => DevelopmentTitleId, // Dev
            Environment.Staging => "12345",          // Staging (configure depois)
            Environment.Production => "54321",       // Produção (configure depois)
            _ => DevelopmentTitleId
        };
    }

    /// <summary>
    /// Retorna o ID do usuário para testes
    /// </summary>
    public static string GetTestUserId()
    {
        if (!PlayerPrefs.HasKey(PersistentCustomIdKey) || PlayerPrefs.GetString(PersistentCustomIdKey) != FixedTestUserId)
        {
            PlayerPrefs.SetString(PersistentCustomIdKey, FixedTestUserId);
            PlayerPrefs.Save();
        }

        return FixedTestUserId;
    }

    /// <summary>
    /// Define se deve criar conta automaticamente
    /// </summary>
    public static bool GetCreateAccountFlag() => true;

    /// <summary>
    /// Limpa o ID persistido para cenários de teste controlados.
    /// </summary>
    public static void ResetTestUserId()
    {
        if (!PlayerPrefs.HasKey(PersistentCustomIdKey))
        {
            return;
        }

        PlayerPrefs.DeleteKey(PersistentCustomIdKey);
        PlayerPrefs.Save();
    }
}
