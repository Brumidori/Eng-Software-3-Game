using System;
using System.Collections.Generic;

[Serializable]
public class AdminDeckCategoryDto
{
    public string nome;
    public string key;
}

[Serializable]
public class AdminDeckIndexDto
{
    public int versao;
    public List<AdminDeckCategoryDto> categorias;
}

[Serializable]
public class AdminDeckRequestDto
{
    public string nome;
    public string key;
    public DeckSchemaV2 deck;
}

public class CloudScriptEnvelope
{
    public bool success;
    public string error;
    public object details;
    public Dictionary<string, object> raw;
}
