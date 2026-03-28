using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class CardTester : MonoBehaviour
{
    private enum EstadoMenu { Menu, CarregandoCategoria, AguardandoResposta, MostrandoResultado }
    private EstadoMenu estadoAtual = EstadoMenu.Menu;
    
    private bool mensagemInicialExibida = false;
    private string categoriaAtual = "";
    private Carta cartaAtual = null;
    
    private Dictionary<string, string> categoriasMap = new Dictionary<string, string>
    {
        { "1", "Ciência" },
        { "2", "Cultura Geral" },
        { "3", "Esportes" },
        { "4", "Geografia" },
        { "5", "História" }
    };

    void Start()
    {
        ExibirMensagemInicial();
    }

    void ExibirMensagemInicial()
    {
        Debug.Log("Pressione ESPAÇO para entrar no menu de categorias");
    }

    void Update()
    {
        if (Keyboard.current == null) return;

        switch (estadoAtual)
        {
            case EstadoMenu.Menu:
                VerificarMenuPrincipal();
                break;
            case EstadoMenu.CarregandoCategoria:
                VerificarSelecaoCategoria();
                break;
            case EstadoMenu.AguardandoResposta:
                VerificarRespostaUsuario();
                break;
            case EstadoMenu.MostrandoResultado:
                VerificarProximaAcao();
                break;
        }
    }

    void VerificarMenuPrincipal()
    {
        if (!mensagemInicialExibida)
        {
            mensagemInicialExibida = true;
        }

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ExibirMenuCategorias();
            estadoAtual = EstadoMenu.CarregandoCategoria;
        }
    }

    void VerificarSelecaoCategoria()
    {
        var keyboard = Keyboard.current;

        for (int i = 1; i <= 5; i++)
        {
            Key key = ((i == 1) ? Key.Digit1 :
                      (i == 2) ? Key.Digit2 :
                      (i == 3) ? Key.Digit3 :
                      (i == 4) ? Key.Digit4 :
                      Key.Digit5);

            if (keyboard[key].wasPressedThisFrame)
            {
                string numero = i.ToString();
                if (categoriasMap.ContainsKey(numero))
                {
                    categoriaAtual = categoriasMap[numero];
                    CarregarCategoria(categoriaAtual);
                }
                return;
            }
        }

        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            Debug.Log("Seleção cancelada.");
            estadoAtual = EstadoMenu.Menu;
        }
    }

    void ExibirMenuCategorias()
    {
        Debug.Log("========== ESCOLHA UMA CATEGORIA ==========");
        Debug.Log("[1] Ciência");
        Debug.Log("[2] Cultura Geral");
        Debug.Log("[3] Esportes");
        Debug.Log("[4] Geografia");
        Debug.Log("[5] História");
        Debug.Log("==========================================");
    }

    void CarregarCategoria(string categoria)
    {
        Debug.Log($"Carregando categoria: {categoria}");
        DeckManager.Instance.LoadDeck(categoria);

        Invoke(nameof(ExibirCarta), 1f);
    }

    void ExibirCarta()
    {
        cartaAtual = DeckManager.Instance.GetCarta(categoriaAtual);

        if (cartaAtual != null)
        {
            if (cartaAtual.alternativas != null && cartaAtual.alternativas.Count > 0)
            {
                Debug.Log("========== CARTA DE " + categoriaAtual.ToUpper() + " ==========");
                Debug.Log("Pergunta: " + cartaAtual.pergunta);

                for (int i = 0; i < cartaAtual.alternativas.Count; i++)
                {
                    Debug.Log("[" + i + "] " + cartaAtual.alternativas[i]);
                }

                Debug.Log("Escolha uma alternativa (0-" + (cartaAtual.alternativas.Count - 1) + "):");
                Debug.Log("==========================================");

                estadoAtual = EstadoMenu.AguardandoResposta;
            }
            else
            {
                Debug.LogWarning("Carta sem alternativas!");
                estadoAtual = EstadoMenu.Menu;
            }
        }
        else
        {
            Debug.LogError("Erro ao carregar carta!");
            estadoAtual = EstadoMenu.Menu;
        }
    }

    void VerificarRespostaUsuario()
    {
        var keyboard = Keyboard.current;

        for (int i = 0; i < 10; i++)
        {
            Key key = ((i == 0) ? Key.Digit0 :
                      (i == 1) ? Key.Digit1 :
                      (i == 2) ? Key.Digit2 :
                      (i == 3) ? Key.Digit3 :
                      (i == 4) ? Key.Digit4 :
                      (i == 5) ? Key.Digit5 :
                      (i == 6) ? Key.Digit6 :
                      (i == 7) ? Key.Digit7 :
                      (i == 8) ? Key.Digit8 :
                      Key.Digit9);

            if (keyboard[key].wasPressedThisFrame)
            {
                if (i < cartaAtual.alternativas.Count)
                {
                    ValidarResposta(i);
                    estadoAtual = EstadoMenu.MostrandoResultado;
                }
                else
                {
                    Debug.LogWarning("Alternativa inválida! Escolha entre 0-" + (cartaAtual.alternativas.Count - 1));
                }
                return;
            }
        }
    }

    void ValidarResposta(int respostaEscolhida)
    {
        if (respostaEscolhida == cartaAtual.respostaCorreta)
        {
            Debug.Log("<color=green>✓ RESPOSTA CORRETA!</color>");
        }
        else
        {
            Debug.Log("<color=red>✗ RESPOSTA ERRADA!</color>");
            Debug.Log("Sua resposta: [" + respostaEscolhida + "] " + cartaAtual.alternativas[respostaEscolhida]);
            Debug.Log("Resposta correta: [" + cartaAtual.respostaCorreta + "] " + cartaAtual.alternativas[cartaAtual.respostaCorreta]);
        }
        Debug.Log("Pressione ESPAÇO para próxima carta ou ESC para voltar ao menu");
    }

    void VerificarProximaAcao()
    {
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ExibirCarta();
            estadoAtual = EstadoMenu.AguardandoResposta;
        }
        else if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Debug.Log("Voltando ao menu de categorias...");
            ExibirMenuCategorias();
            estadoAtual = EstadoMenu.CarregandoCategoria;
        }
    }
}