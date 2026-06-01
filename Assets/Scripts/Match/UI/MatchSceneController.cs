using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PlayFab.ClientModels;
using BrainDuel.Match;
using BrainDuel.Match.Core;
using BrainDuel.Match.PowerUps;
using BrainDuel.Network;

namespace BrainDuel.Match.UI
{
    public class MatchSceneController : MonoBehaviour
    {
        // ----------------------------------------------------------
        // Referências obrigatórias
        // ----------------------------------------------------------

        [Header("Sistemas")]
        [SerializeField] private MatchStateMachine stateMachine;
        [SerializeField] private PowerUpManager    powerUpManager;

        // ----------------------------------------------------------
        // HUD Permanente
        // ----------------------------------------------------------

        [Header("HUD")]
        [SerializeField] private TMP_Text nomeJogador1Text;
        [SerializeField] private TMP_Text nivelJogador1Text;
        [SerializeField] private Image    hpBarFillJogador1;
        [SerializeField] private TMP_Text nomeJogador2Text;
        [SerializeField] private TMP_Text nivelJogador2Text;
        [SerializeField] private Image    hpBarFillJogador2;
        [SerializeField] private TMP_Text rodadaText;

        // ----------------------------------------------------------
        // Panel: Tema e Poderes
        // ----------------------------------------------------------

        [Header("Panel Tema/Poderes")]
        [SerializeField] private GameObject panelTemaPoderes;
        [SerializeField] private Image      temaIcon;
        [SerializeField] private Button[]   powerUpButtons;       // 5 botões na ordem: Escudo, EscudoDuplo, Roubo, Aposta, Eliminar2
        [SerializeField] private string[]   powerUpItemIds;       // IDs no catálogo PlayFab, mesma ordem dos botões
        [SerializeField] private TMP_Text   powerUpDescricaoText;
        [SerializeField] private TMP_Text   timerTemaPoderesText;

        // ----------------------------------------------------------
        // Panel: Pergunta
        // ----------------------------------------------------------

        [Header("Panel Pergunta")]
        [SerializeField] private GameObject panelPergunta;
        [SerializeField] private Image      cardTemaPerguntaImage;
        [SerializeField] private TMP_Text   perguntaText;
        [SerializeField] private Button[]   opcaoButtons;         // 4 botões de resposta
        [SerializeField] private TMP_Text[] opcaoTexts;           // textos dos 4 botões
        [SerializeField] private TMP_Text   timerPerguntaText;

        // ----------------------------------------------------------
        // Panel: Reveal
        // ----------------------------------------------------------

        [Header("Panel Reveal")]
        [SerializeField] private GameObject panelReveal;
        [SerializeField] private TMP_Text   escolhaJogador1Text;
        [SerializeField] private TMP_Text   escolhaJogador2Text;
        [SerializeField] private TMP_Text   damagePopupText;

        // ----------------------------------------------------------
        // Panel: Fim de Partida
        // ----------------------------------------------------------

        [Header("Panel Fim de Partida")]
        [SerializeField] private GameObject panelFimPartida;

        [Header("Sub-panel Vitória")]
        [SerializeField] private GameObject panelVitoria;
        [SerializeField] private TMP_Text   xpGanhoVitoriaText;
        [SerializeField] private Button     btnMenuVitoria;
        [SerializeField] private Button     btnOutraPartidaVitoria;

        [Header("Sub-panel Derrota")]
        [SerializeField] private GameObject panelDerrota;
        [SerializeField] private TMP_Text   xpGanhoDerrotaText;
        [SerializeField] private Button     btnMenuDerrota;
        [SerializeField] private Button     btnOutraPartidaDerrota;

        // ----------------------------------------------------------
        // Estado interno
        // ----------------------------------------------------------

        // Ordem dos botões de poder no PoderesGrid (deve bater com a ordem visual)
        private static readonly PowerUpType[] OrdemPoderes =
        {
            PowerUpType.SimpleShield,
            PowerUpType.DoubleShield,
            PowerUpType.Steal,
            PowerUpType.Bet,
            PowerUpType.EliminateTwo
        };

        private MatchContext              _ctx;
        private QuestionRevealPayload     _perguntaAtual;
        private Coroutine                 _timerCoroutine;
        private bool                      _poderJaUsado;
        private Dictionary<PowerUpType, int> _poderesNoInventario = new Dictionary<PowerUpType, int>();
        private string                    _currentThemeName;

        // ----------------------------------------------------------
        // Unity lifecycle
        // ----------------------------------------------------------

        void Start()
        {
            if (stateMachine == null)
                stateMachine = FindObjectOfType<MatchStateMachine>();

            _ctx = stateMachine.Context;

            stateMachine.OnPhaseChanged        += HandlePhaseChanged;
            stateMachine.OnHPUpdated           += HandleHPAtualizado;
            stateMachine.OnRoundResultReceived += HandleResultadoRodada;
            stateMachine.OnMatchEnded          += HandleFimPartida;
            stateMachine.OnRoundStarted        += HandleRodadaIniciada;
            stateMachine.OnQuestionRevealed    += HandlePerguntaRevelada;

            if (powerUpManager != null)
                powerUpManager.OnPowerUpActivated += HandlePoderAtivado;

            InventoryService.OnInventoryLoaded += PopularInventarioPoderes;

            // Garante que o InventoryService existe mesmo sem ter passado pelas cenas anteriores
            if (InventoryService.Instance == null)
                new GameObject("InventoryService").AddComponent<InventoryService>();
            InventoryService.Instance.LoadInventory();

            ConfigurarBotoesPoder();
            ConfigurarBotoesResposta();
            if (btnMenuVitoria        != null) btnMenuVitoria.onClick.AddListener(IrParaMenu);
            if (btnMenuDerrota        != null) btnMenuDerrota.onClick.AddListener(IrParaMenu);
            if (btnOutraPartidaVitoria != null) btnOutraPartidaVitoria.onClick.AddListener(IrParaMatchmaking);
            if (btnOutraPartidaDerrota != null) btnOutraPartidaDerrota.onClick.AddListener(IrParaMatchmaking);

            InicializarHUD();
            DesativarTodosPanels();
        }

        void OnDestroy()
        {
            if (stateMachine == null) return;

            stateMachine.OnPhaseChanged        -= HandlePhaseChanged;
            stateMachine.OnHPUpdated           -= HandleHPAtualizado;
            stateMachine.OnRoundResultReceived -= HandleResultadoRodada;
            stateMachine.OnMatchEnded          -= HandleFimPartida;
            stateMachine.OnRoundStarted        -= HandleRodadaIniciada;
            stateMachine.OnQuestionRevealed    -= HandlePerguntaRevelada;

            if (powerUpManager != null)
                powerUpManager.OnPowerUpActivated -= HandlePoderAtivado;

            InventoryService.OnInventoryLoaded -= PopularInventarioPoderes;
        }

        // ----------------------------------------------------------
        // Inicialização
        // ----------------------------------------------------------

        void InicializarHUD()
        {
            if (_ctx == null) return;

            // Fallback: se o contexto não tiver nome, usa o perfil local carregado
            string nomeLocal = _ctx.LocalDisplayName;
            if (string.IsNullOrEmpty(nomeLocal))
                nomeLocal = PlayerDataService.Instance?.CurrentProfile?.displayName ?? "Você";

            SetTMPText(nomeJogador1Text,  nomeLocal);
            SetTMPText(nivelJogador1Text, $"Nv. {_ctx.LocalLevel}");

            string nomeOponente = _ctx.OpponentDisplayName;
            if (string.IsNullOrEmpty(nomeOponente)) nomeOponente = "Adversário";
            SetTMPText(nomeJogador2Text,  nomeOponente);
            SetTMPText(nivelJogador2Text, _ctx.OpponentLevel > 0 ? $"Nv. {_ctx.OpponentLevel}" : "Nv. ?");

            AtualizarBarrasHP(_ctx.LocalHP, _ctx.OpponentHP);
            AtualizarTextoRodada();
        }

        void DesativarTodosPanels()
        {
            panelTemaPoderes.SetActive(false);
            panelPergunta.SetActive(false);
            panelReveal.SetActive(false);
            panelFimPartida.SetActive(false);
        }

        // ----------------------------------------------------------
        // Troca de fase
        // ----------------------------------------------------------

        void HandlePhaseChanged(MatchPhase fase)
        {
            DesativarTodosPanels();
            PararTimer();

            switch (fase)
            {
                case MatchPhase.ThemeAndPowerUp:
                    InicializarHUD();   // atualiza nomes/nível/rodada toda vez que a rodada começa
                    panelTemaPoderes.SetActive(true);
                    IniciarTimer(timerTemaPoderesText, MatchConfig.ThemePhaseDurationMs / 1000f, IrParaPergunta);
                    AtualizarTextoRodada();
                    break;

                case MatchPhase.Question:
                    panelPergunta.SetActive(true);
                    IniciarTimer(timerPerguntaText, MatchConfig.QuestionPhaseDurationMs / 1000f);
                    break;

                case MatchPhase.Reveal:
                    panelReveal.SetActive(true);
                    break;

                case MatchPhase.MatchEnd:
                    panelFimPartida.SetActive(true);
                    break;
            }
        }

        // ----------------------------------------------------------
        // HUD
        // ----------------------------------------------------------

        void HandleHPAtualizado(int localHP, int oponenteHP)
        {
            AtualizarBarrasHP(localHP, oponenteHP);
        }

        void AtualizarBarrasHP(int localHP, int oponenteHP)
        {
            hpBarFillJogador1.fillAmount = localHP   / (float)MatchConfig.InitialHP;
            hpBarFillJogador2.fillAmount = oponenteHP / (float)MatchConfig.InitialHP;
        }

        void AtualizarTextoRodada()
        {
            if (_ctx == null) return;
            SetTMPText(rodadaText, $"{_ctx.CurrentRound} / {MatchConfig.MaxRounds}");
        }

        static void SetTMPText(TMP_Text label, string text)
        {
            if (label == null) return;
            label.text                = text;
            label.color               = Color.white;
            label.enableVertexGradient = false;
        }

        // ----------------------------------------------------------
        // Tema (fase ThemeAndPowerUp)
        // ----------------------------------------------------------

        void HandleRodadaIniciada(RoundStartPayload payload)
        {
            _currentThemeName = payload.ThemeName;

            // Inicializa o PowerUpManager na primeira rodada (ServerState já está populado)
            if (payload.RoundNumber == 1 && powerUpManager != null)
                powerUpManager.Initialize(_ctx, stateMachine, _ctx.EquippedPowerUp);

            ExibirSpriteTema(payload.ThemeName);
            AtualizarEstadoBotoesPoder();
        }

        void ExibirSpriteTema(string themeName)
        {
            if (temaIcon == null) return;
            Sprite sprite = Resources.Load<Sprite>($"Temas/Tema-{themeName}");
            if (sprite != null)
                temaIcon.sprite = sprite;
            else
                Debug.LogWarning($"[Match] Sprite não encontrado: Temas/Tema-{themeName}");
        }

        void ExibirCardTema(string themeName)
        {
            if (cardTemaPerguntaImage == null || string.IsNullOrEmpty(themeName)) return;
            Sprite sprite = Resources.Load<Sprite>($"Temas/Card{themeName}");
            if (sprite != null)
                cardTemaPerguntaImage.sprite = sprite;
            else
                Debug.LogWarning($"[Match] Card de tema não encontrado: Temas/Card{themeName}");
        }

        // ----------------------------------------------------------
        // Poderes
        // ----------------------------------------------------------

        void ConfigurarBotoesPoder()
        {
            if (powerUpButtons == null) return;
            for (int i = 0; i < powerUpButtons.Length; i++)
            {
                if (powerUpButtons[i] == null) continue;
                int idx = i;
                powerUpButtons[i].onClick.AddListener(() => OnPoderClicado(idx));
            }
        }

        void PopularInventarioPoderes(List<ItemInstance> itens)
        {
            _poderesNoInventario.Clear();

            // Conta quantidades por tipo usando mapeamento do Inspector (powerUpItemIds)
            if (powerUpItemIds != null)
            {
                for (int i = 0; i < OrdemPoderes.Length; i++)
                {
                    if (i >= powerUpItemIds.Length) break;
                    string itemId = powerUpItemIds[i];
                    int count = 0;
                    foreach (var it in itens)
                        if (it.ItemId == itemId) count++;
                    if (count > 0)
                        _poderesNoInventario[OrdemPoderes[i]] = count;
                }
            }

            // Se o EquippedPowerUp no contexto ainda é None (perfil não estava disponível
            // quando a partida inicializou), resolve agora que o inventário carregou
            if (_ctx?.LocalPlayer != null && _ctx.LocalPlayer.EquippedPowerUp == PowerUpType.None)
            {
                var equipado = ResolveEquippedPowerUpFromProfile();

                // Último recurso: usa o primeiro poder encontrado no inventário
                if (equipado == PowerUpType.None)
                {
                    foreach (var kv in _poderesNoInventario)
                    {
                        if (kv.Key != PowerUpType.None) { equipado = kv.Key; break; }
                    }
                }

                if (equipado != PowerUpType.None)
                {
                    _ctx.LocalPlayer.EquippedPowerUp = equipado;
                    powerUpManager?.Initialize(_ctx, stateMachine, equipado);
                    Debug.Log($"[Match] EquippedPowerUp resolvido após inventário: {equipado}");
                }
            }

            // Garante que o poder equipado aparece no inventário visual (mesmo sem item cadastrado)
            if (_ctx != null && _ctx.EquippedPowerUp != PowerUpType.None)
            {
                if (!_poderesNoInventario.ContainsKey(_ctx.EquippedPowerUp))
                    _poderesNoInventario[_ctx.EquippedPowerUp] = 1;
            }

            AtualizarQuantidadesTexto();

            if (panelTemaPoderes.activeSelf)
                AtualizarEstadoBotoesPoder();
        }

        private static PowerUpType ResolveEquippedPowerUpFromProfile()
        {
            var raw = PlayerDataService.Instance?.CurrentProfile?.equippedPowerUp;
            if (!string.IsNullOrWhiteSpace(raw) &&
                Enum.TryParse<PowerUpType>(raw, ignoreCase: true, out var t) &&
                t != PowerUpType.None)
                return t;
            return PowerUpType.None;
        }

        void AtualizarQuantidadesTexto()
        {
            if (powerUpButtons == null) return;
            for (int i = 0; i < powerUpButtons.Length; i++)
            {
                if (powerUpButtons[i] == null) continue;
                PowerUpType tipo = i < OrdemPoderes.Length ? OrdemPoderes[i] : PowerUpType.None;
                var label = powerUpButtons[i].GetComponentInChildren<TMP_Text>();
                if (label == null) continue;
                if (_poderesNoInventario.TryGetValue(tipo, out int qtd))
                    label.text = $"x{qtd}";
                else
                    label.text = string.Empty;
            }
        }

        void AtualizarEstadoBotoesPoder()
        {
            // Poder já usado nesta partida — bloqueia todos para sempre
            if (_poderJaUsado)
            {
                BloquearTodosBotoesPoder();
                return;
            }

            if (_ctx == null) return;
            if (powerUpButtons == null) return;

            bool podeUsar = _ctx.LocalPlayer != null && !_ctx.LocalPlayer.HasUsedPowerUp;

            for (int i = 0; i < powerUpButtons.Length; i++)
            {
                if (powerUpButtons[i] == null) continue;

                PowerUpType tipo         = i < OrdemPoderes.Length ? OrdemPoderes[i] : PowerUpType.None;
                bool        temInventario = _poderesNoInventario.ContainsKey(tipo);

                bool habilitado = temInventario && podeUsar;

                powerUpButtons[i].interactable = habilitado;

                float alpha = temInventario ? 1f : 0.15f;
                SetAlpha(powerUpButtons[i], alpha);
            }

            if (powerUpDescricaoText != null)
                powerUpDescricaoText.text = string.Empty;
        }

        void OnPoderClicado(int index)
        {
            if (_poderJaUsado) return;
            if (index >= OrdemPoderes.Length) return;

            PowerUpType tipo = OrdemPoderes[index];

            // Desabilita os outros botões imediatamente ao clicar
            for (int i = 0; i < powerUpButtons.Length; i++)
            {
                if (i != index)
                {
                    powerUpButtons[i].interactable = false;
                    SetAlpha(powerUpButtons[i], 0.2f);
                }
            }

            if (powerUpDescricaoText != null)
                powerUpDescricaoText.text = PowerUpManager.GetDescription(tipo);

            powerUpManager?.TryActivate(tipo);
        }

        void HandlePoderAtivado(PowerUpType tipo)
        {
            _poderJaUsado = true;
            BloquearTodosBotoesPoder();
            if (powerUpDescricaoText != null)
                powerUpDescricaoText.text = $"{PowerUpManager.GetName(tipo)} ativado!";
        }

        void BloquearTodosBotoesPoder()
        {
            if (powerUpButtons == null) return;
            foreach (var btn in powerUpButtons)
            {
                if (btn == null) continue;
                btn.interactable = false;
                SetAlpha(btn, 0.2f);
            }
        }

        // ----------------------------------------------------------
        // Pergunta (fase Question)
        // ----------------------------------------------------------

        void ConfigurarBotoesResposta()
        {
            if (opcaoButtons == null) return;
            for (int i = 0; i < opcaoButtons.Length; i++)
            {
                if (opcaoButtons[i] == null) continue;
                int idx = i;
                opcaoButtons[i].onClick.AddListener(() => OnRespostaClicada(idx));
            }
        }

        void HandlePerguntaRevelada(QuestionRevealPayload payload)
        {
            _perguntaAtual = payload;
            if (perguntaText != null)
            {
                perguntaText.text               = payload.QuestionText;
                perguntaText.color              = Color.black;
                perguntaText.enableVertexGradient = false;
            }

            ExibirCardTema(_currentThemeName);

            if (opcaoButtons == null || opcaoTexts == null) return;
            for (int i = 0; i < opcaoButtons.Length && i < payload.Answers.Length; i++)
            {
                if (opcaoButtons[i] == null) continue;
                if (opcaoTexts != null && i < opcaoTexts.Length && opcaoTexts[i] != null)
                    opcaoTexts[i].text = payload.Answers[i].Text;

                bool eliminada = payload.Answers[i].IsEliminated;
                opcaoButtons[i].interactable = !eliminada;
                SetAlpha(opcaoButtons[i], eliminada ? 0.2f : 1f);
            }
        }

        void OnRespostaClicada(int index)
        {
            if (_perguntaAtual == null || index >= _perguntaAtual.Answers.Length) return;

            string answerId = _perguntaAtual.Answers[index].Id;
            stateMachine.SubmitAnswer(answerId);

            if (opcaoButtons != null)
                foreach (var btn in opcaoButtons)
                    if (btn != null) btn.interactable = false;
        }

        // ----------------------------------------------------------
        // Reveal
        // ----------------------------------------------------------

        void HandleResultadoRodada(RoundResultPayload payload)
        {
            if (_ctx == null) return;

            var localResult    = _ctx.GetLocalResult(payload);
            var oponenteResult = _ctx.GetOpponentResult(payload);

            if (escolhaJogador1Text != null)
                escolhaJogador1Text.text = $"Você: {TextoResposta(localResult.AnsweredId)}";
            if (escolhaJogador2Text != null)
                escolhaJogador2Text.text = $"Adversário: {TextoResposta(oponenteResult.AnsweredId)}";

            if (damagePopupText == null) return;

            bool acertou = localResult.Result == AnswerResult.Correct;
            int  dano    = localResult.DamageDealt;

            if (acertou)
            {
                damagePopupText.text  = $"ACERTOU!  -{dano} HP";
                damagePopupText.color = Color.green;
            }
            else
            {
                damagePopupText.text  = "Errou!";
                damagePopupText.color = Color.red;
            }
        }

        string TextoResposta(string answerId)
        {
            if (_perguntaAtual == null || string.IsNullOrEmpty(answerId)) return "—";

            foreach (var opt in _perguntaAtual.Answers)
                if (opt.Id == answerId) return opt.Text;

            return "—";
        }

        // ----------------------------------------------------------
        // Fim de partida
        // ----------------------------------------------------------

        void HandleFimPartida(MatchEndPayload payload)
        {
            bool venceu = payload.WinnerId == _ctx?.LocalPlayerId;
            bool porAbandono = payload.Reason == MatchEndReason.Abandonment;

            MostrarResultadoFinal(venceu, porAbandono);
        }

        // Chamado pelo AbandonarPartidaModal quando o jogador LOCAL abandona
        public void MostrarDerrotaPorAbandono()
        {
            DesativarTodosPanels();
            panelFimPartida.SetActive(true);
            MostrarResultadoFinal(venceu: false, porAbandono: true);
        }

        void MostrarResultadoFinal(bool venceu, bool porAbandono)
        {
            if (panelFimPartida != null) panelFimPartida.SetActive(true);
            if (panelVitoria   != null) panelVitoria.SetActive(venceu);
            if (panelDerrota   != null) panelDerrota.SetActive(!venceu);

            if (venceu)
            {
                if (xpGanhoVitoriaText != null)
                    xpGanhoVitoriaText.text = porAbandono ? "+20 XP  +40 moedas" : "+100 XP";
            }
            else
            {
                if (xpGanhoDerrotaText != null)
                    xpGanhoDerrotaText.text = porAbandono ? "-10 XP" : "+20 XP";
            }
        }

        void IrParaMenu()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("HomeScreen");
        }

        void IrParaMatchmaking()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MatchMaking");
        }

        // ----------------------------------------------------------
        // Timer
        // ----------------------------------------------------------

        void IniciarTimer(TMP_Text label, float duracao, Action aoTerminar = null)
        {
            _timerCoroutine = StartCoroutine(TimerRoutine(label, duracao, aoTerminar));
        }

        void PararTimer()
        {
            if (_timerCoroutine != null)
            {
                StopCoroutine(_timerCoroutine);
                _timerCoroutine = null;
            }
        }

        IEnumerator TimerRoutine(TMP_Text label, float duracao, Action aoTerminar)
        {
            if (label != null) { label.text = Mathf.CeilToInt(duracao).ToString(); label.color = Color.white; }
            float restante = duracao;
            while (restante > 0f)
            {
                if (label != null)
                {
                    label.text  = Mathf.CeilToInt(restante).ToString();
                    label.color = restante <= 2f ? Color.red : Color.white;
                }
                restante -= Time.deltaTime;
                yield return null;
            }
            if (label != null) { label.text = "0"; label.color = Color.red; }
            aoTerminar?.Invoke();
        }

        void IrParaPergunta()
        {
            // Só age se o servidor ainda não trocou a fase (evita conflito)
            if (stateMachine.Phase == MatchPhase.ThemeAndPowerUp)
            {
                panelTemaPoderes.SetActive(false);
                panelPergunta.SetActive(true);
                IniciarTimer(timerPerguntaText, MatchConfig.QuestionPhaseDurationMs / 1000f);
            }
        }

        // ----------------------------------------------------------
        // Utilitário
        // ----------------------------------------------------------

        static void SetAlpha(Button btn, float alpha)
        {
            var cg = btn.GetComponent<CanvasGroup>();
            if (cg == null) cg = btn.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = alpha;
        }
    }
}
