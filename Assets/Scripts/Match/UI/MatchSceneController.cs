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
        private HashSet<PowerUpType>      _poderesNoInventario = new HashSet<PowerUpType>();

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
            InventoryService.Instance?.LoadInventory();

            ConfigurarBotoesPoder();
            ConfigurarBotoesResposta();
            btnMenuVitoria.onClick.AddListener(IrParaMenu);
            btnMenuDerrota.onClick.AddListener(IrParaMenu);
            btnOutraPartidaVitoria.onClick.AddListener(IrParaMatchmaking);
            btnOutraPartidaDerrota.onClick.AddListener(IrParaMatchmaking);

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

            nomeJogador1Text.text = _ctx.LocalDisplayName;
            nomeJogador2Text.text = _ctx.OpponentDisplayName;
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
            if (_ctx != null)
                rodadaText.text = $"{_ctx.CurrentRound} / {MatchConfig.MaxRounds}";
        }

        // ----------------------------------------------------------
        // Tema (fase ThemeAndPowerUp)
        // ----------------------------------------------------------

        void HandleRodadaIniciada(RoundStartPayload payload)
        {
            ExibirSpriteTema(payload.ThemeName);
            AtualizarEstadoBotoesPoder();
        }

        void ExibirSpriteTema(string themeName)
        {
            Sprite sprite = Resources.Load<Sprite>($"Temas/Tema-{themeName}");
            if (sprite != null)
                temaIcon.sprite = sprite;
            else
                Debug.LogWarning($"[Match] Sprite não encontrado: Temas/Tema-{themeName}");
        }

        // ----------------------------------------------------------
        // Poderes
        // ----------------------------------------------------------

        void ConfigurarBotoesPoder()
        {
            for (int i = 0; i < powerUpButtons.Length; i++)
            {
                int idx = i;
                powerUpButtons[i].onClick.AddListener(() => OnPoderClicado(idx));
            }
        }

        void PopularInventarioPoderes(List<ItemInstance> itens)
        {
            _poderesNoInventario.Clear();

            for (int i = 0; i < OrdemPoderes.Length; i++)
            {
                if (i >= powerUpItemIds.Length) break;

                string itemId = powerUpItemIds[i];
                bool possuiItem = itens.Exists(it => it.ItemId == itemId);

                if (possuiItem)
                    _poderesNoInventario.Add(OrdemPoderes[i]);
            }

            // Atualiza os botões caso o panel já esteja visível
            if (panelTemaPoderes.activeSelf)
                AtualizarEstadoBotoesPoder();
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

            PowerUpType equipado = _ctx.EquippedPowerUp;
            bool        podeUsar = _ctx.CanUsePowerUp;

            for (int i = 0; i < powerUpButtons.Length; i++)
            {
                PowerUpType tipo          = OrdemPoderes[i];
                bool        esteEquipado  = tipo == equipado;
                bool        temInventario = _poderesNoInventario.Contains(tipo);
                bool        habilitado    = esteEquipado && podeUsar && temInventario;

                powerUpButtons[i].interactable = habilitado;
                SetAlpha(powerUpButtons[i], temInventario ? (esteEquipado ? 1f : 0.35f) : 0.15f);
            }

            powerUpDescricaoText.text = string.Empty;
        }

        void OnPoderClicado(int index)
        {
            if (_poderJaUsado) return;
            if (index >= OrdemPoderes.Length) return;

            // Desabilita os outros botões imediatamente ao clicar
            for (int i = 0; i < powerUpButtons.Length; i++)
            {
                if (i != index)
                {
                    powerUpButtons[i].interactable = false;
                    SetAlpha(powerUpButtons[i], 0.2f);
                }
            }

            PowerUpType tipo = OrdemPoderes[index];
            powerUpDescricaoText.text = PowerUpManager.GetDescription(tipo);
            powerUpManager?.TryActivate();
        }

        void HandlePoderAtivado(PowerUpType tipo)
        {
            _poderJaUsado = true;
            BloquearTodosBotoesPoder();
            powerUpDescricaoText.text = $"{PowerUpManager.GetName(tipo)} ativado!";
        }

        void BloquearTodosBotoesPoder()
        {
            foreach (var btn in powerUpButtons)
            {
                btn.interactable = false;
                SetAlpha(btn, 0.2f);
            }
        }

        // ----------------------------------------------------------
        // Pergunta (fase Question)
        // ----------------------------------------------------------

        void ConfigurarBotoesResposta()
        {
            for (int i = 0; i < opcaoButtons.Length; i++)
            {
                int idx = i;
                opcaoButtons[i].onClick.AddListener(() => OnRespostaClicada(idx));
            }
        }

        void HandlePerguntaRevelada(QuestionRevealPayload payload)
        {
            _perguntaAtual   = payload;
            perguntaText.text = payload.QuestionText;

            for (int i = 0; i < opcaoButtons.Length && i < payload.Answers.Length; i++)
            {
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

            foreach (var btn in opcaoButtons)
                btn.interactable = false;
        }

        // ----------------------------------------------------------
        // Reveal
        // ----------------------------------------------------------

        void HandleResultadoRodada(RoundResultPayload payload)
        {
            if (_ctx == null) return;

            var localResult    = _ctx.GetLocalResult(payload);
            var oponenteResult = _ctx.GetOpponentResult(payload);

            escolhaJogador1Text.text = $"Você: {TextoResposta(localResult.AnsweredId)}";
            escolhaJogador2Text.text = $"Adversário: {TextoResposta(oponenteResult.AnsweredId)}";

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
            panelFimPartida.SetActive(true);
            panelVitoria.SetActive(venceu);
            panelDerrota.SetActive(!venceu);

            if (venceu)
            {
                // Vitória por abandono do adversário: recompensa diferente
                xpGanhoVitoriaText.text = porAbandono ? "+20 XP  +40 moedas" : "+100 XP";
            }
            else
            {
                // Derrota por abandono: penalidade
                xpGanhoDerrotaText.text = porAbandono ? "-10 XP" : "+20 XP";
            }
        }

        void IrParaMenu()
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
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
            float restante = duracao;
            while (restante > 0f)
            {
                label.text  = Mathf.CeilToInt(restante).ToString();
                label.color = restante <= 2f ? Color.red : Color.white;
                restante   -= Time.deltaTime;
                yield return null;
            }
            label.text = "0";
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
