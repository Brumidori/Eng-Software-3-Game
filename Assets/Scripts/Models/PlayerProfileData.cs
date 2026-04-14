using UnityEngine;

[System.Serializable]
public class PlayerProfileData
{
    public int level = 1;
    public int currentXp = 0;
    public PlayerBasicSettings settings = new PlayerBasicSettings();

    public static PlayerProfileData CreateDefault()
    {
        return new PlayerProfileData();
    }
}

[System.Serializable]
public class PlayerBasicSettings
{
    public bool soundEnabled = true;
    public float masterVolume = 1f;
    public string language = "pt-BR";
}