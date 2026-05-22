using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class PlayerProfileData
{
    public string displayName = "JOGADOR";
    public string avatarId = "";
    public string avatarUrl = "";
    public int level = 1;
    public int currentXp = 0;
    public int xpToNextLevel = 100;
    public int wins = 0;
    public int losses = 0;
    public float accuracy = 0f;
    public string title = "NOVATO";
    public int brainCoins = 0;
    public string equippedDeckId = "";
    public List<PlayerDeckData> decks = new List<PlayerDeckData>();
    public PlayerBasicSettings settings = new PlayerBasicSettings();

    public static PlayerProfileData CreateDefault()
    {
        return new PlayerProfileData();
    }

    public static PlayerProfileData CreatePlaceholder()
    {
        return new PlayerProfileData
        {
            displayName = "JOGADOR ALFA",
            avatarId = "avatar_01",
            level = 20,
            currentXp = 15000,
            xpToNextLevel = 20000,
            wins = 150,
            losses = 50,
            accuracy = 78f,
            title = "MESTRE DO TRIVIA",
            brainCoins = 13500,
            equippedDeckId = "ENTRETENIMENTO",
            decks = new List<PlayerDeckData>
            {
                new PlayerDeckData { id = "HISTORIA", category = "HISTORIA", colorHex = "#8B2020", iconName = "history", isEquipped = false },
                new PlayerDeckData { id = "ENTRETENIMENTO", category = "ENTRETENIMENTO", colorHex = "#D4A017", iconName = "entertainment", isEquipped = true },
                new PlayerDeckData { id = "GEOGRAFIA", category = "GEOGRAFIA", colorHex = "#1E5D9A", iconName = "geography", isEquipped = false },
                new PlayerDeckData { id = "CIENCIA", category = "CIENCIA", colorHex = "#2E8B57", iconName = "science", isEquipped = false }
            }
        };
    }
}

[System.Serializable]
public class PlayerDeckData
{
    public string id;
    public string category;
    public string colorHex;
    public string iconName;
    public bool isEquipped;
}

[System.Serializable]
public class PlayerBasicSettings
{
    public bool soundEnabled = true;
    public float masterVolume = 1f;
    public string language = "pt-BR";
}