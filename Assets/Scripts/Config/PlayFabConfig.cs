using UnityEngine;

public static class PlayFabConfig
{
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
            Environment.Development => "15571",      // Dev
            Environment.Staging => "12345",          // Staging (configure depois)
            Environment.Production => "54321",       // Produção (configure depois)
            _ => "15571"
        };
    }

    /// <summary>
    /// Retorna o ID do usuário para testes
    /// </summary>
    public static string GetTestUserId()
    {
        return $"test_user_{System.DateTime.Now.Ticks}";
    }

    /// <summary>
    /// Define se deve criar conta automaticamente
    /// </summary>
    public static bool GetCreateAccountFlag() => true;
}
