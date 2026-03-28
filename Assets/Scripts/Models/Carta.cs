using System.Collections.Generic;

[System.Serializable]
public class Carta
{
    public string id;
    public string pergunta;
    public List<string> alternativas;
    public int respostaCorreta;
    public string categoria;
    public string dificuldade;
}

[System.Serializable]
public class DeckWrapper
{
    public List<Carta> deck;
}