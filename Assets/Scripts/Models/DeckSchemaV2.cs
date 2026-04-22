using System.Collections.Generic;

[System.Serializable]
public class DeckSchemaV2
{
    public string deck_id;
    public string theme;
    public List<DeckQuestionV2> questions;
}

[System.Serializable]
public class DeckQuestionV2
{
    public string id;
    public string text;
    public List<DeckOptionV2> options;
    public int time_limit;
}

[System.Serializable]
public class DeckOptionV2
{
    public string text;
    public bool is_correct;
}
