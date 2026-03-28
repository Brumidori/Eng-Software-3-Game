using System.Collections.Generic;

[System.Serializable]
public class DeckIndex
{
    public int versao;
    public List<CategoriaInfo> categorias;
}

[System.Serializable]
public class CategoriaInfo
{
    public string nome;
    public string key;
}