// ============================================================
// ReconnectionManager.cs — trata desconexão e reconexão do
// jogador local, além de monitorar o timeout do oponente.
//
// Fluxo de reconexão (jogador local):
//   1. App retoma (OnApplicationFocus / OnApplicationPause)
//   2. Detecta que match ainda está ativo no servidor
//   3. Chama CloudScript RejoinMatch para obter estado atual
//   4. Recebe ReconnectSync via Party com ServerMatchState completo
//   5. MatchStateMachine restaura fase correta
//
// Fluxo de abandono do oponente:
//   1. Party detecta peer desconectado → OnOpponentDisconnected
//   2. Servidor aguarda ReconnectWindowMs (30 s)
//   3. Se não reconectar → CloudScript declara vitória ao local
//   4. MatchEnd chega via Party broadcast
// ============================================================
using System.Collections;
using UnityEngine;
using BrainDuel.Match;
using BrainDuel.Match.Network;
using BrainDuel.Network;

namespace BrainDuel.Match.Core
{
    [RequireComponent(typeof(MatchStateMachine))]
    public class ReconnectionManager : MonoBehaviour
    {
        [SerializeField] private float _reconnectPollIntervalSeconds = 3f;

        private MatchStateMachine _machine;
        private MatchContext      _context;
        private Coroutine         _reconnectCoroutine;
        private bool              _isReconnecting;

        // ----------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------

        private void Awake()
        {
            _machine = GetComponent<MatchStateMachine>();
        }

        public void Initialize(MatchContext context)
        {
            _context = context;
        }

        // ----------------------------------------------------------
        // Reconexão do jogador LOCAL
        // ----------------------------------------------------------

        private void OnApplicationPause(bool paused)
        {
            if (paused) return;
            if (_context == null || string.IsNullOrEmpty(_context.MatchId)) return;

            // App voltou ao primeiro plano — verifica se match ainda está ativo
            AttemptRejoin();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus) return;
            if (_context == null || string.IsNullOrEmpty(_context.MatchId)) return;
            AttemptRejoin();
        }

        private void AttemptRejoin()
        {
            if (_isReconnecting) return;
            _isReconnecting = true;

            CloudScriptClient.Call("RejoinMatch", new
            {
                matchId  = _context.MatchId,
                playerId = _context.LocalPlayerId
            }, onSuccess: result =>
            {
                _isReconnecting = false;
                // ReconnectSync chegará via Party broadcast — MatchStateMachine o trata
                Debug.Log("[Reconnect] RejoinMatch confirmado");
            }, onError: err =>
            {
                _isReconnecting = false;
                Debug.LogError($"[Reconnect] Falha ao rejuntar: {err}");
                // Retry
                _reconnectCoroutine = StartCoroutine(RetryRejoin());
            });
        }

        private IEnumerator RetryRejoin()
        {
            yield return new WaitForSeconds(_reconnectPollIntervalSeconds);
            AttemptRejoin();
        }

        // ----------------------------------------------------------
        // Reconexão do OPONENTE (chamado pela MatchStateMachine)
        // ----------------------------------------------------------

        public void OnSyncReceived(ReconnectSyncPayload payload)
        {
            Debug.Log("[Reconnect] Estado sincronizado com servidor");
            _isReconnecting = false;
            if (_reconnectCoroutine != null) StopCoroutine(_reconnectCoroutine);

            // Restaura fase correta via transição normal da state machine
            _machine.TransitionTo(payload.FullState.Phase);
        }

        // ----------------------------------------------------------
        // Monitoramento de AFK do oponente
        // ----------------------------------------------------------

        // A lógica de timeout do oponente é SERVER-SIDE (CloudScript AfkWatchdog).
        // O cliente apenas exibe o contador regressivo que recebe via Party.
        // Se o servidor declarar abandono, chegará OpponentAbandoned → MatchEnd.
    }
}
