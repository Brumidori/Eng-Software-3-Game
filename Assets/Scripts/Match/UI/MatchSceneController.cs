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
        [SerializeField] private GameObject    panelReveal;
        // 4 RectTransforms dos slots de resposta no card do reveal (ordem A-B-C-D)
        [SerializeField] private RectTransform[] revealOpcaoSlots;
        // Deslocamento horizontal para separar J1 (esquerda) e J2 (direita) quando escolhem o mesmo slot
        [SerializeField] private float revealIndicadorOffsetX = 40f;
        // Imagens-indicador que são repositionadas sobre o slot certo
        [SerializeField] private Image          respostaCorretaImage;   // RespostaCorreta
        [SerializeField] private Image          escolhaJogador1Image;   // Escolhajogador1
        [SerializeField] private Image          escolhaJogador2Image;   // Escolhajogador2
        [SerializeField] private TMP_Text       danoJogador1Text;
        [SerializeField] private TMP_Text       danoJogador2Text;
        [SerializeField] private TMP_Text       damagePopupText;         // ComboPop — resultado do jogador local
        [SerializeField] private TMP_Text       damagePopupJogador2Text; // resultado do oponente

        // ----------------------------------------------------------
        // Panel: Fim de Partida
        // ----------------------------------------------------------

        [Header("Panel Fim de Partida")]
        [SerializeField] private GameObject panelFimPartida;

        [Header("Sub-panel Vitória")]
        [SerializeField] private GameObject panelVitoria;
        [SerializeField] private TMP_Text   xpGanhoVitoriaText;
        [SerializeField] private TMP_Text   moedaVitoriaText;
        [SerializeField] private Button     btnMenuVitoria;
        [SerializeField] private Button     btnOutraPartidaVitoria;

        [Header("Sub-panel Derrota")]
        [SerializeField] private GameObject panelDerrota;
        [SerializeField] private TMP_Text   xpGanhoDerrotaText;
        [SerializeField] private TMP_Text   moedaDerrotaText;
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
                stateMachine = FindAnyObjectByType<MatchStateMachine>();

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
            // Reveal: mantém o panelPergunta congelado como fundo — apenas sobrepõe o reveal
            if (fase == MatchPhase.Reveal)
            {
                CongelarPainelPergunta();  // garante timer=0 e botões bloqueados
                panelReveal.SetActive(true);
                return;
            }

            DesativarTodosPanels();
            PararTimer();

            switch (fase)
            {
                case MatchPhase.ThemeAndPowerUp:
                    InicializarHUD();
                    panelTemaPoderes.SetActive(true);
                    IniciarTimer(timerTemaPoderesText, MatchConfig.ThemePhaseDurationMs / 1000f, IrParaPergunta);
                    AtualizarTextoRodada();
                    break;

                case MatchPhase.Question:
                    panelPergunta.SetActive(true);
                    // Ao chegar em 0 congela o painel (botões + timer) sem escondê-lo
                    IniciarTimer(timerPerguntaText, MatchConfig.QuestionPhaseDurationMs / 1000f, CongelarPainelPergunta);
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
            Debug.Log($"[HUD] HP atualizado → local={localHP} oponente={oponenteHP}");
            AtualizarBarrasHP(localHP, oponenteHP);
        }

        void AtualizarBarrasHP(int localHP, int oponenteHP)
        {
            if (hpBarFillJogador1 == null)
                Debug.LogWarning("[HUD] hpBarFillJogador1 não está referenciado no Inspector!");
            else
                hpBarFillJogador1.fillAmount = localHP / (float)MatchConfig.InitialHP;

            if (hpBarFillJogador2 == null)
                Debug.LogWarning("[HUD] hpBarFillJogador2 não está referenciado no Inspector!");
            else
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
            var key    = themeName?.Replace(" ", "") ?? string.Empty;
            Sprite sprite = Resources.Load<Sprite>($"Temas/Tema-{key}");
            if (sprite != null)
                temaIcon.sprite = sprite;
            else
                Debug.LogWarning($"[Match] Sprite não encontrado: Temas/Tema-{key}");
        }

        void ExibirCardTema(string themeName)
        {
            if (cardTemaPerguntaImage == null || string.IsNullOrEmpty(themeName)) return;
            var key    = themeName.Replace(" ", "");
            Sprite sprite = Resources.Load<Sprite>($"Temas/Card{key}");
            if (sprite != null)
                cardTemaPerguntaImage.sprite = sprite;
            else
                Debug.LogWarning($"[Match] Card de tema não encontrado: Temas/Card{key}");
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

            // Apenas desabilita os botões — o timer continua contando.
            // O panel só é congelado quando ambos respondem (HandlePhaseChanged Reveal)
            // ou o tempo esgota (callback do timer → CongelarPainelPergunta).
            if (opcaoButtons != null)
                foreach (var btn in opcaoButtons)
                    if (btn != null) btn.interactable = false;
        }

        // Congela o painel de pergunta: timer em 0, botões não-interativos.
        // Chamado ao responder OU quando o tempo esgota.
        void CongelarPainelPergunta()
        {
            PararTimer();
            if (timerPerguntaText != null)
            {
                timerPerguntaText.text                = "0";
                timerPerguntaText.color               = Color.red;
                timerPerguntaText.enableVertexGradient = false;
            }
            if (opcaoButtons != null)
                foreach (var btn in opcaoButtons)
                    if (btn != null) btn.interactable = false;

            // Em modo real, solicita ao servidor que processe a rodada (caso de timeout)
            stateMachine?.TriggerProcessRound();
        }

        // ----------------------------------------------------------
        // Reveal
        // ----------------------------------------------------------

        void HandleResultadoRodada(RoundResultPayload payload)
        {
            if (_ctx == null) return;

            var localResult    = _ctx.GetLocalResult(payload);
            var oponenteResult = _ctx.GetOpponentResult(payload);

            // Posiciona indicadores sobre os slots de resposta
            int correctIdx = AnswerIdToIndex(payload.CorrectAnswerId);
            int j1Idx      = AnswerIdToIndex(localResult.AnsweredId);
            int j2Idx      = AnswerIdToIndex(oponenteResult.AnsweredId);

            // Resposta correta: centro do slot
            MoverIndicador(respostaCorretaImage, correctIdx, ativo: true,  offsetX: 0f);
            // Cérebros: posicionados sobre o slot escolhido, deslocados para não sobrepor
            MoverIndicador(escolhaJogador1Image, j1Idx, ativo: localResult.Result    != AnswerResult.NotAnswered, offsetX: -revealIndicadorOffsetX);
            MoverIndicador(escolhaJogador2Image, j2Idx, ativo: oponenteResult.Result != AnswerResult.NotAnswered, offsetX:  revealIndicadorOffsetX);

            // Dano sofrido por cada jogador (HPBefore − HPAfter do receptor)
            int danoRecebido1 = localResult.HPBefore    - localResult.HPAfter;
            int danoRecebido2 = oponenteResult.HPBefore - oponenteResult.HPAfter;

            if (danoJogador1Text != null)
            {
                danoJogador1Text.text                = danoRecebido1 > 0 ? $"-{danoRecebido1} HP" : "0 HP";
                danoJogador1Text.color               = danoRecebido1 > 0 ? Color.red : Color.white;
                danoJogador1Text.enableVertexGradient = false;
            }
            if (danoJogador2Text != null)
            {
                danoJogador2Text.text                = danoRecebido2 > 0 ? $"-{danoRecebido2} HP" : "0 HP";
                danoJogador2Text.color               = danoRecebido2 > 0 ? Color.red : Color.white;
                danoJogador2Text.enableVertexGradient = false;
            }

            // Popup resultado — jogador local
            ExibirPopupResultado(damagePopupText,         localResult.Result);
            // Popup resultado — oponente
            ExibirPopupResultado(damagePopupJogador2Text, oponenteResult.Result);
        }

        static void ExibirPopupResultado(TMP_Text label, AnswerResult resultado)
        {
            if (label == null) return;
            bool acertou     = resultado == AnswerResult.Correct;
            bool semResposta = resultado == AnswerResult.NotAnswered;
            label.text                = acertou ? "ACERTOU!" : semResposta ? "TEMPO ESGOTADO!" : "ERROU!";
            label.color               = acertou ? Color.green : Color.red;
            label.enableVertexGradient = false;
        }

        // Move a imagem-indicador para cima do slot cujo índice é slotIdx.
        // offsetX desloca horizontalmente (negativo = esquerda, positivo = direita).
        void MoverIndicador(Image indicador, int slotIdx, bool ativo, float offsetX = 0f)
        {
            if (indicador == null) return;
            bool valido = ativo
                && slotIdx >= 0
                && revealOpcaoSlots != null
                && slotIdx < revealOpcaoSlots.Length
                && revealOpcaoSlots[slotIdx] != null;

            indicador.gameObject.SetActive(valido);
            if (valido)
            {
                Vector3 pos = revealOpcaoSlots[slotIdx].position;
                pos.x += offsetX;
                indicador.transform.position = pos;
            }
        }


        static void TintarIndicador(Image indicador, AnswerResult resultado)
        {
            if (indicador == null || !indicador.gameObject.activeSelf) return;
            indicador.color = resultado switch
            {
                AnswerResult.Correct   => new Color(0.4f, 1f, 0.4f),
                AnswerResult.Incorrect => new Color(1f, 0.4f, 0.4f),
                _                      => Color.white
            };
        }

        // "A" → 0, "B" → 1, "C" → 2, "D" → 3; qualquer outro → -1
        static int AnswerIdToIndex(string id)
        {
            if (string.IsNullOrEmpty(id) || id.Length != 1) return -1;
            char c = char.ToUpperInvariant(id[0]);
            return (c >= 'A' && c <= 'D') ? c - 'A' : -1;
        }

        // ----------------------------------------------------------
        // Fim de partida
        // ----------------------------------------------------------

        void HandleFimPartida(MatchEndPayload payload)
        {
            bool semVencedor  = string.IsNullOrEmpty(payload.WinnerId);
            bool porAbandono  = payload.Reason == MatchEndReason.Abandonment;
            // Derrota dupla: ninguém ganhou E foi por abandono → ambos perdem
            bool derrotaDupla = semVencedor && porAbandono;
            bool empate       = semVencedor && !porAbandono;
            bool venceu       = !semVencedor && payload.WinnerId == _ctx?.LocalPlayerId;

            MostrarResultadoFinal(venceu, porAbandono, empate, derrotaDupla);
        }

        // Chamado pelo AbandonarPartidaModal quando o jogador LOCAL abandona
        public void MostrarDerrotaPorAbandono()
        {
            DesativarTodosPanels();
            panelFimPartida.SetActive(true);
            MostrarResultadoFinal(venceu: false, porAbandono: true, empate: false, derrotaDupla: false);
        }

        void MostrarResultadoFinal(bool venceu, bool porAbandono, bool empate = false, bool derrotaDupla = false)
        {
            if (panelFimPartida != null) panelFimPartida.SetActive(true);

            // Derrota dupla (ambos AFK): ambos veem painel de derrota
            bool mostrarVitoria = (venceu || empate) && !derrotaDupla;
            if (panelVitoria != null) panelVitoria.SetActive(mostrarVitoria);
            if (panelDerrota != null) panelDerrota.SetActive(!mostrarVitoria);

            int xpGanho, moedas;

            if (mostrarVitoria)
            {
                xpGanho = empate ? 50 : porAbandono ? 50  : 100;
                moedas  = empate ? 20 : porAbandono ? 40  : 80;

                SetTMPText(xpGanhoVitoriaText, $"+{xpGanho} XP");
                SetTMPText(moedaVitoriaText,   $"+{moedas}");
            }
            else
            {
                // derrotaDupla → ambos AFK, penalidade máxima
                xpGanho = derrotaDupla ? -20 : porAbandono ? -10 : 20;
                moedas  = 0;

                SetTMPText(xpGanhoDerrotaText, xpGanho >= 0 ? $"+{xpGanho} XP" : $"{xpGanho} XP");
                SetTMPText(moedaDerrotaText,   "0");
            }

            // Salva resultado no PlayFab — atualiza ranking e estatísticas
            SalvarResultadoNoPlayFab(xpGanho, venceu, empate && !derrotaDupla);
        }

        private void SalvarResultadoNoPlayFab(int xpGanho, bool venceu, bool empate)
        {
            // Atualiza leaderboard e contadores de vitória/partida
            if (RankingService.Instance != null)
                RankingService.Instance.SalvarFimDePartida(xpGanho, venceu || empate);
            else
                Debug.LogWarning("[Match] RankingService não encontrado — estatísticas não salvas.");

            // Atualiza XP no player_profile
            var perfil = PlayerDataService.Instance?.CurrentProfile;
            if (perfil != null && xpGanho != 0)
            {
                int novoXp = Mathf.Max(0, perfil.currentXp + xpGanho);
                PlayerDataService.Instance.SaveProgress(perfil.level, novoXp);
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
