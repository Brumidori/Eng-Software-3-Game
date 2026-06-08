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

        [Header("Skins")]
        [SerializeField] private Image player1SkinImage;
        [SerializeField] private Image player2SkinImage;
        [SerializeField] private System.Collections.Generic.List<NamedSprite> avatarSprites = new System.Collections.Generic.List<NamedSprite>();

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

        [Header("Power-up Ativado")]
        // Imagens que exibem o sprite do PU ativado por cada jogador
        [SerializeField] private Image   powerUpAtivadoJogador1Image;
        [SerializeField] private Image   powerUpAtivadoJogador2Image;

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
        [SerializeField] private TMP_Text       comboText;               // combo do jogador local
        [SerializeField] private TMP_Text       comboJogador2Text;       // combo do oponente

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
        private bool                      _eliminateTwoPendente;   // EliminateTwo ativado aguardando pergunta
        private Dictionary<PowerUpType, int> _poderesNoInventario = new Dictionary<PowerUpType, int>();
        private List<PlayFab.ClientModels.ItemInstance> _inventarioItens = new List<PlayFab.ClientModels.ItemInstance>();
        private string                    _currentThemeName;

        // ----------------------------------------------------------
        // Unity lifecycle
        // ----------------------------------------------------------

        void Start()
        {
            if (stateMachine == null)
                stateMachine = FindAnyObjectByType<MatchStateMachine>();

            _ctx = stateMachine.Context;

            stateMachine.OnPhaseChanged              += HandlePhaseChanged;
            stateMachine.OnHPUpdated                 += HandleHPAtualizado;
            stateMachine.OnRoundResultReceived       += HandleResultadoRodada;
            stateMachine.OnMatchEnded                += HandleFimPartida;
            stateMachine.OnRoundStarted              += HandleRodadaIniciada;
            stateMachine.OnQuestionRevealed          += HandlePerguntaRevelada;
            stateMachine.OnPowerUpActivatedReceived  += HandlePowerUpOponente;
            MatchEvents.OnEliminateTwo               += AplicarEliminateDuas;

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

            // Power-up ativado: só aparece após ativação
            if (powerUpAtivadoJogador1Image != null) powerUpAtivadoJogador1Image.gameObject.SetActive(false);
            if (powerUpAtivadoJogador2Image != null) powerUpAtivadoJogador2Image.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (stateMachine == null) return;

            stateMachine.OnPhaseChanged              -= HandlePhaseChanged;
            stateMachine.OnHPUpdated                 -= HandleHPAtualizado;
            stateMachine.OnRoundResultReceived       -= HandleResultadoRodada;
            stateMachine.OnMatchEnded                -= HandleFimPartida;
            stateMachine.OnRoundStarted              -= HandleRodadaIniciada;
            stateMachine.OnQuestionRevealed          -= HandlePerguntaRevelada;
            stateMachine.OnPowerUpActivatedReceived  -= HandlePowerUpOponente;
            MatchEvents.OnEliminateTwo               -= AplicarEliminateDuas;

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
            
            // Carregar skins
            AplicarSkin(_ctx.LocalAvatarId, player1SkinImage, false);
            AplicarSkin(_ctx.OpponentAvatarId, player2SkinImage, true);
        }

        void AplicarSkin(string avatarId, Image targetImage, bool flipHorizontal)
        {
            if (targetImage == null)
            {
                Debug.LogWarning($"[MatchScene] targetImage is NULL para avatarId='{avatarId}' flip={flipHorizontal}");
                return;
            }
            
            string idToLoad = string.IsNullOrWhiteSpace(avatarId) ? "skinDefault" : avatarId;
            Debug.Log($"[MatchScene] AplicarSkin chamado com avatarId='{avatarId}' -> idToLoad='{idToLoad}' flip={flipHorizontal}");

            Sprite sprite = Resources.Load<Sprite>($"AvatarImages/{idToLoad}");
            
            if (sprite == null && avatarSprites != null)
            {
                foreach (var namedSprite in avatarSprites)
                {
                    if (namedSprite != null && namedSprite.IsMatch(idToLoad))
                    {
                        sprite = namedSprite.sprite;
                        Debug.Log($"[MatchScene] Skin '{idToLoad}' encontrada no avatarSprites.");
                        break;
                    }
                }
            }

            if (sprite != null)
            {
                targetImage.sprite = sprite;
                targetImage.color = Color.white;
                Debug.Log($"[MatchScene] Skin '{idToLoad}' aplicada com sucesso!");
            }
            else
            {
                Debug.LogWarning($"[MatchScene] ALERTA: Skin '{idToLoad}' NÃO FOI ENCONTRADA. A lista avatarSprites tem {avatarSprites?.Count ?? 0} itens.");
            }
            
            if (flipHorizontal)
            {
                targetImage.rectTransform.localScale = new Vector3(-1, 1, 1);
            }
            else
            {
                targetImage.rectTransform.localScale = new Vector3(1, 1, 1);
            }
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
            _eliminateTwoPendente = false;
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
            _inventarioItens = itens ?? new List<ItemInstance>();
            _poderesNoInventario.Clear();

            // Usa _inventarioItens (garantidamente não-null) em vez do parâmetro itens
            if (powerUpItemIds != null)
            {
                for (int i = 0; i < OrdemPoderes.Length; i++)
                {
                    if (i >= powerUpItemIds.Length) break;
                    string itemId = powerUpItemIds[i];
                    int count = 0;
                    foreach (var it in _inventarioItens)
                        if (it != null && it.ItemId == itemId) count++;
                    if (count > 0)
                        _poderesNoInventario[OrdemPoderes[i]] = count;
                }
            }

            // Cacheia LocalPlayer uma vez — é uma propriedade que reavalia ServerState a cada acesso.
            // Sem cache, pode retornar null na segunda chamada se ServerState mudar entre as avaliações.
            var localPlayer = _ctx?.LocalPlayer;
            if (localPlayer != null && localPlayer.EquippedPowerUp == PowerUpType.None)
            {
                var equipado = ResolveEquippedPowerUpFromProfile();

                if (equipado == PowerUpType.None)
                {
                    foreach (var kv in _poderesNoInventario)
                    {
                        if (kv.Key != PowerUpType.None) { equipado = kv.Key; break; }
                    }
                }

                if (equipado != PowerUpType.None)
                {
                    localPlayer.EquippedPowerUp = equipado;
                    powerUpManager?.Initialize(_ctx, stateMachine, equipado);
                    Debug.Log($"[Match] EquippedPowerUp resolvido após inventário: {equipado}");
                }
            }

            if (_ctx != null && _ctx.EquippedPowerUp != PowerUpType.None)
            {
                if (!_poderesNoInventario.ContainsKey(_ctx.EquippedPowerUp))
                    _poderesNoInventario[_ctx.EquippedPowerUp] = 1;
            }

            if (_ctx?.IsStubMode == true)
            {
                foreach (PowerUpType tipo in OrdemPoderes)
                    if (tipo != PowerUpType.None && !_poderesNoInventario.ContainsKey(tipo))
                        _poderesNoInventario[tipo] = 1;
            }

            AtualizarQuantidadesTexto();

            // Guarda nula antes de chamar activeSelf — campo Inspector pode não estar conectado
            if (panelTemaPoderes != null && panelTemaPoderes.activeSelf)
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

            // Mostra sprite do PU ativado pelo jogador local
            ExibirSpritePowerUp(powerUpAtivadoJogador1Image, tipo);

            // EliminateTwo será aplicado quando a pergunta chegar
            if (tipo == PowerUpType.EliminateTwo)
                _eliminateTwoPendente = true;

            // Consome 1 unidade do item no inventário PlayFab
            ConsumirPowerUpDoInventario(tipo);
        }

        // Sprite do PU do OPONENTE (recebido via Party broadcast)
        void HandlePowerUpOponente(PowerUpActivatedPayload payload)
        {
            if (payload == null) return;
            // Ignora se for o próprio jogador local — J1 já foi tratado em HandlePoderAtivado
            string localId = _ctx?.LocalPlayerId ?? PlayFab.PlayFabSettings.staticPlayer?.EntityId;
            if (!string.IsNullOrEmpty(localId) && payload.PlayerId == localId) return;

            ExibirSpritePowerUp(powerUpAtivadoJogador2Image, payload.PowerUp);
        }

        // Mostra o sprite do PU copiando a imagem do botão correspondente (sem array extra)
        void ExibirSpritePowerUp(Image destino, PowerUpType tipo)
        {
            if (destino == null) return;
            if (tipo == PowerUpType.None) { destino.gameObject.SetActive(false); return; }

            // Copia o sprite do botão do painel de poderes que corresponde ao tipo
            Sprite sprite = null;
            if (powerUpButtons != null)
            {
                for (int i = 0; i < OrdemPoderes.Length && i < powerUpButtons.Length; i++)
                {
                    if (OrdemPoderes[i] != tipo || powerUpButtons[i] == null) continue;
                    sprite = powerUpButtons[i].GetComponent<Image>()?.sprite;
                    break;
                }
            }

            destino.gameObject.SetActive(true);
            if (sprite != null) destino.sprite = sprite;
            destino.color = Color.white;
        }

        // Elimina 2 respostas aleatórias (efeito visual do EliminateTwo)
        void AplicarEliminateDuas(int[] indices)
        {
            if (opcaoButtons == null || indices == null) return;
            foreach (var idx in indices)
            {
                if (idx < 0 || idx >= opcaoButtons.Length || opcaoButtons[idx] == null) continue;
                opcaoButtons[idx].interactable = false;
                SetAlpha(opcaoButtons[idx], 0.2f);
                if (_perguntaAtual != null && idx < _perguntaAtual.Answers.Length)
                    _perguntaAtual.Answers[idx].IsEliminated = true;
            }
        }

        void ConsumirPowerUpDoInventario(PowerUpType tipo)
        {
            if (tipo == PowerUpType.None) return;

            // Encontra o ItemId do catálogo correspondente ao PU
            string catalogItemId = null;
            for (int i = 0; i < OrdemPoderes.Length; i++)
            {
                if (OrdemPoderes[i] == tipo && i < powerUpItemIds.Length)
                { catalogItemId = powerUpItemIds[i]; break; }
            }
            if (string.IsNullOrEmpty(catalogItemId)) return;

            // Encontra a instância no inventário e consome
            var instancia = _inventarioItens.Find(it => it.ItemId == catalogItemId);
            if (instancia == null)
            {
                Debug.LogWarning($"[Match] Item '{catalogItemId}' não encontrado no inventário para consumo.");
                return;
            }

            PlayFab.PlayFabClientAPI.ConsumeItem(
                new PlayFab.ClientModels.ConsumeItemRequest { ItemInstanceId = instancia.ItemInstanceId, ConsumeCount = 1 },
                _ =>
                {
                    Debug.Log($"[Match] Power-up '{catalogItemId}' consumido do inventário.");
                    // Atualiza SOMENTE este poder no contador local — não recarrega o inventário
                    // (evita zerar todos os poderes por recarga completa durante a partida)
                    if (_poderesNoInventario.TryGetValue(tipo, out int qtd))
                    {
                        if (qtd > 1) _poderesNoInventario[tipo] = qtd - 1;
                        else         _poderesNoInventario.Remove(tipo);
                        AtualizarQuantidadesTexto();
                    }
                    // Remove do cache local para não consumir duas vezes se chamado novamente
                    _inventarioItens.RemoveAll(it => it.ItemInstanceId == instancia.ItemInstanceId);
                },
                e => Debug.LogWarning($"[Match] Falha ao consumir power-up: {e.GenerateErrorReport()}")
            );
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

            // Aplica EliminateTwo agora que os botões estão visíveis
            if (_eliminateTwoPendente)
            {
                _eliminateTwoPendente = false;

                if (payload.EliminatedIndices != null && payload.EliminatedIndices.Length > 0)
                {
                    // Modo real: servidor já calculou 2 índices errados
                    AplicarEliminateDuas(payload.EliminatedIndices);
                }
                else
                {
                    // Modo stub: usa a resposta correta exposta pelo estado para evitar eliminá-la
                    string correctId = stateMachine?.CurrentStubCorrectAnswerId;
                    AplicarEliminateDuas(EscolherDoisErrados(payload.Answers, correctId));
                }
            }
        }

        // Escolhe 2 índices de respostas erradas, nunca eliminando a correta
        static int[] EscolherDoisErrados(AnswerOption[] answers, string correctId)
        {
            int correctIdx = -1;
            if (!string.IsNullOrEmpty(correctId))
                for (int i = 0; i < answers.Length; i++)
                    if (answers[i].Id == correctId) { correctIdx = i; break; }

            var errados = new List<int>();
            for (int i = 0; i < answers.Length; i++)
                if (i != correctIdx) errados.Add(i);

            var escolhidos = new List<int>();
            while (escolhidos.Count < 2 && errados.Count > 0)
            {
                int pick = UnityEngine.Random.Range(0, errados.Count);
                escolhidos.Add(errados[pick]);
                errados.RemoveAt(pick);
            }
            return escolhidos.ToArray();
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
        // ProcessRound é responsabilidade do QuestionState — não disparar aqui.
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
            // oponenteResult.WasShielded → escudo do jogador LOCAL bloqueou ataque do oponente
            // localResult.WasShielded    → escudo do OPONENTE bloqueou ataque do jogador local
            int danoRecebido1 = localResult.HPBefore    - localResult.HPAfter;
            int danoRecebido2 = oponenteResult.HPBefore - oponenteResult.HPAfter;

            ExibirTextoDano(danoJogador1Text, danoRecebido1, oponenteResult.WasShielded);
            ExibirTextoDano(danoJogador2Text, danoRecebido2, localResult.WasShielded);

            // Popup resultado — jogador local
            ExibirPopupResultado(damagePopupText,         localResult.Result);
            // Popup resultado — oponente
            ExibirPopupResultado(damagePopupJogador2Text, oponenteResult.Result);

            // Combo de cada jogador
            ExibirCombo(comboText,         localResult.StreakAfter);
            ExibirCombo(comboJogador2Text, oponenteResult.StreakAfter);
        }

        static void ExibirCombo(TMP_Text label, int streak)
        {
            if (label == null) return;

            int bonus    = DamageConfig.GetStreakBonus(streak);
            bool temCombo = streak >= 2 && bonus > 0;

            label.gameObject.SetActive(temCombo);
            if (!temCombo) return;

            label.text                = $"COMBO x{streak}\n+{bonus} Dano";
            label.color               = Color.yellow;
            label.enableVertexGradient = false;
        }

        static void ExibirTextoDano(TMP_Text label, int dano, bool bloqueadoPorEscudo)
        {
            if (label == null) return;
            if (bloqueadoPorEscudo)
            {
                label.text                = "Dano Bloqueado - Escudo";
                label.color               = new Color(0.4f, 0.7f, 1f); // azul claro
                label.enableVertexGradient = false;
                return;
            }
            label.text                = dano > 0 ? $"-{dano} HP" : "0 HP";
            label.color               = dano > 0 ? Color.red : Color.white;
            label.enableVertexGradient = false;
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
                SetTMPText(moedaVitoriaText,   $"+{moedas} moedas");
            }
            else
            {
                // derrotaDupla → ambos AFK, penalidade máxima
                xpGanho = derrotaDupla ? -20 : porAbandono ? -10 : 20;
                moedas  = 0;

                SetTMPText(xpGanhoDerrotaText, xpGanho >= 0 ? $"+{xpGanho} XP" : $"{xpGanho} XP");
                SetTMPText(moedaDerrotaText,   moedas > 0 ? $"+{moedas} moedas" : "0 moedas");
            }

            // Salva resultado no PlayFab — atualiza ranking e estatísticas
            SalvarResultadoNoPlayFab(xpGanho, venceu, empate && !derrotaDupla);
        }

        private void SalvarResultadoNoPlayFab(int xpGanho, bool venceu, bool empate)
        {
            // Modo stub: CloudScript não roda — cliente atualiza as estatísticas diretamente
            // Modo real: CloudScript já chamou updatePlayerStats ao fim da partida, sem double-counting
            if (_ctx?.IsStubMode == true)
            {
                if (RankingService.Instance != null)
                    RankingService.Instance.SalvarFimDePartida(xpGanho, venceu || empate);
                else
                    Debug.LogWarning("[Match] RankingService não encontrado — estatísticas não salvas.");
            }

            // Atualiza XP no player_profile para exibição imediata na UI de perfil
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
