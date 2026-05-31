// Descomente após instalar o PlayFab Party SDK:
// #define PLAYFAB_PARTY_INSTALLED

// ============================================================
// PartyNetworkManager.cs — wrapper sobre PlayFab Party SDK.
//
// DEPENDÊNCIA: PlayFab Party Unity SDK instalado em
//   Assets/PlayFabPartySDK/  (namespace PlayFab.Party)
//
// Se o SDK ainda não estiver instalado, os blocos
// #if PLAYFAB_PARTY_INSTALLED ficarão inativos e o projeto
// compilará com stubs para desenvolvimento de UI.
// ============================================================
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PlayFab.Json;
using BrainDuel.Network;
using BrainDuel.Match;
#if PLAYFAB_PARTY_INSTALLED
using PlayFab.Party;
#endif

namespace BrainDuel.Match.Network
{
    public class PartyNetworkManager : MonoBehaviour
    {
        public static PartyNetworkManager Instance { get; private set; }

        // ----------------------------------------------------------
        // Eventos tipados — a MatchStateMachine assina estes
        // ----------------------------------------------------------
        public event Action<RoundStartPayload>       OnRoundStart;
        public event Action<QuestionRevealPayload>   OnQuestionReveal;
        public event Action<OpponentAnsweredPayload> OnOpponentAnswered;
        public event Action<RoundResultPayload>      OnRoundResult;
        public event Action<MatchEndPayload>         OnMatchEnd;
        public event Action<PowerUpActivatedPayload> OnPowerUpActivated;
        public event Action<ReconnectSyncPayload>    OnReconnectSync;
        public event Action<string>                  OnOpponentDisconnected;
        public event Action<string>                  OnOpponentReconnected;
        public event Action<string>                  OnOpponentAbandoned;
        public event Action                          OnNetworkReady;
        public event Action<string>                  OnNetworkError;

        // ----------------------------------------------------------
        // Estado público
        // ----------------------------------------------------------
        public bool   IsConnected      { get; private set; }
        public string LocalPlayerId    { get; private set; }
        public long   AverageLatencyMs { get; private set; }

        // True quando rodando sem o PlayFab Party SDK instalado
        public bool IsStubMode
        {
            get
            {
#if PLAYFAB_PARTY_INSTALLED
                return false;
#else
                return true;
#endif
            }
        }

        // ----------------------------------------------------------
        // Estado interno
        // ----------------------------------------------------------
        private string     _networkDescriptor;
        private int        _sequenceCounter;
        private List<long> _latencySamples  = new List<long>(12);
        private Coroutine  _pingCoroutine;

#if PLAYFAB_PARTY_INSTALLED
        private PlayFabMultiplayerManager _partyManager;
        private PlayFabNetwork            _currentNetwork;
#endif

        private const float PingIntervalSeconds = 5f;

        // ----------------------------------------------------------
        // Lifecycle
        // ----------------------------------------------------------

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if PLAYFAB_PARTY_INSTALLED
            _partyManager = PlayFabMultiplayerManager.Get();
            _partyManager.Initialize(PlayFab.PlayFabSettings.staticSettings.TitleId);
#endif
        }

        private void OnDestroy() => Disconnect();

        // ----------------------------------------------------------
        // Criar rede (primeiro jogador a ser confirmado pelo servidor)
        // ----------------------------------------------------------

        public void CreateNetwork(string matchId, Action<string> onCreated, Action<string> onError)
        {
            LocalPlayerId = PlayFab.PlayFabSettings.staticPlayer.EntityId;

#if PLAYFAB_PARTY_INSTALLED
            var config = new PlayFabNetworkConfiguration
            {
                MaxDeviceCount             = 2,
                MaxDevicesPerUserCount     = 1,
                MaxEndpointsPerDeviceCount = 1,
                MaxUserCount               = 2,
                MaxUsersPerDeviceCount     = 1,
                DirectPeerConnectivityOptions = DirectPeerConnectivityOptions.None
            };

            _partyManager.CreateAndJoinNetwork(config, network =>
            {
                _currentNetwork      = network;
                _networkDescriptor   = network.Descriptor;
                IsConnected          = true;
                SubscribeToEvents();
                StartPingLoop();
                onCreated?.Invoke(_networkDescriptor);
                OnNetworkReady?.Invoke();
                Debug.Log($"[Party] Rede criada: {_networkDescriptor}");
            },
            err =>
            {
                Debug.LogError($"[Party] CreateNetwork falhou: {err}");
                onError?.Invoke(err);
            });
#else
            // Stub para desenvolvimento sem SDK
            _networkDescriptor = $"stub_party_{matchId}";
            IsConnected        = true;
            StartPingLoop();
            onCreated?.Invoke(_networkDescriptor);
            OnNetworkReady?.Invoke();
#endif
        }

        // ----------------------------------------------------------
        // Entrar em rede existente (segundo jogador)
        // ----------------------------------------------------------

        public void JoinNetwork(string networkDescriptor, Action onJoined, Action<string> onError)
        {
            LocalPlayerId      = PlayFab.PlayFabSettings.staticPlayer.EntityId;
            _networkDescriptor = networkDescriptor;

#if PLAYFAB_PARTY_INSTALLED
            _partyManager.JoinNetwork(networkDescriptor, network =>
            {
                _currentNetwork = network;
                IsConnected     = true;
                SubscribeToEvents();
                StartPingLoop();
                onJoined?.Invoke();
                OnNetworkReady?.Invoke();
                Debug.Log($"[Party] Entrou na rede: {networkDescriptor}");
            },
            err =>
            {
                Debug.LogError($"[Party] JoinNetwork falhou: {err}");
                onError?.Invoke(err);
            });
#else
            IsConnected = true;
            StartPingLoop();
            onJoined?.Invoke();
            OnNetworkReady?.Invoke();
#endif
        }

        // ----------------------------------------------------------
        // Desconexão
        // ----------------------------------------------------------

        public void Disconnect()
        {
            if (!IsConnected) return;
            IsConnected = false;
            StopPingLoop();

#if PLAYFAB_PARTY_INSTALLED
            UnsubscribeFromEvents();
            if (_currentNetwork != null)
            {
                _partyManager.LeaveNetwork(_currentNetwork, () =>
                {
                    _currentNetwork    = null;
                    _networkDescriptor = null;
                    Debug.Log("[Party] Desconectado");
                });
            }
#endif
        }

        // ----------------------------------------------------------
        // Envio de mensagens
        // ----------------------------------------------------------

        public void Broadcast<T>(MessageType type, T payload, bool reliable = true)
        {
            if (!IsConnected) return;
            var envelope = BuildEnvelope(type, payload);
            SendRaw(PlayFabSimpleJson.SerializeObject(envelope), reliable);
        }

        public void SendPing() => Broadcast(MessageType.Ping, new PingPayload
        {
            ClientTimestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        });

        // ----------------------------------------------------------
        // Internos de envio
        // ----------------------------------------------------------

        private void SendRaw(string json, bool reliable)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(json);

#if PLAYFAB_PARTY_INSTALLED
            if (_currentNetwork == null) return;

            var option = reliable
                ? DeliveryOption.Guaranteed
                : DeliveryOption.BestEffort;

            // Envia para todos os jogadores remotos da rede
            var remotePlayers = PlayFabPlayer.GetAllRemotePlayers();
            if (remotePlayers != null && remotePlayers.Length > 0)
                _currentNetwork.SendDataMessage(bytes, remotePlayers, option);
#else
            Debug.Log($"[Party][stub] → {json.Substring(0, Mathf.Min(100, json.Length))}");
#endif
        }

        // ----------------------------------------------------------
        // Eventos do SDK Party
        // ----------------------------------------------------------

        private void SubscribeToEvents()
        {
#if PLAYFAB_PARTY_INSTALLED
            _partyManager.OnDataMessageNoCopyReceived += HandleDataMessage;
            _partyManager.OnRemotePlayerJoined        += HandleRemotePlayerJoined;
            _partyManager.OnRemotePlayerLeft          += HandleRemotePlayerLeft;
            _partyManager.OnNetworkLeft               += HandleNetworkLeft;
#endif
        }

        private void UnsubscribeFromEvents()
        {
#if PLAYFAB_PARTY_INSTALLED
            _partyManager.OnDataMessageNoCopyReceived -= HandleDataMessage;
            _partyManager.OnRemotePlayerJoined        -= HandleRemotePlayerJoined;
            _partyManager.OnRemotePlayerLeft          -= HandleRemotePlayerLeft;
            _partyManager.OnNetworkLeft               -= HandleNetworkLeft;
#endif
        }

#if PLAYFAB_PARTY_INSTALLED
        // Chamado pelo SDK a cada frame que chega mensagem
        private void HandleDataMessage(object sender, PlayFabPlayer from, byte[] buffer, uint bufferSize)
        {
            try
            {
                var json     = System.Text.Encoding.UTF8.GetString(buffer, 0, (int)bufferSize);
                var envelope = PlayFabSimpleJson.DeserializeObject<NetworkEnvelope>(json);
                DispatchEnvelope(envelope);
            }
            catch (Exception e)
            {
                Debug.LogError($"[Party] Falha ao parsear mensagem: {e.Message}");
            }
        }

        private void HandleRemotePlayerJoined(object sender, PlayFabPlayer player)
        {
            Debug.Log($"[Party] Jogador entrou: {player.EntityKey.Id}");
        }

        private void HandleRemotePlayerLeft(object sender, PlayFabPlayer player,
            PlayFabPlayerLeftReason reason)
        {
            Debug.Log($"[Party] Jogador saiu: {player.EntityKey.Id} | Razão: {reason}");
            OnOpponentDisconnected?.Invoke(player.EntityKey.Id);
        }

        private void HandleNetworkLeft(object sender, PlayFabNetwork network)
        {
            IsConnected = false;
            Debug.Log("[Party] Saiu da rede");
        }
#endif

        // ----------------------------------------------------------
        // Dispatcher de mensagens → eventos tipados
        // ----------------------------------------------------------

        private void DispatchEnvelope(NetworkEnvelope env)
        {
            switch (env.Type)
            {
                case MessageType.RoundStart:
                    OnRoundStart?.Invoke(Deserialize<RoundStartPayload>(env.Payload));
                    break;
                case MessageType.QuestionReveal:
                    OnQuestionReveal?.Invoke(Deserialize<QuestionRevealPayload>(env.Payload));
                    break;
                case MessageType.OpponentAnswered:
                    OnOpponentAnswered?.Invoke(Deserialize<OpponentAnsweredPayload>(env.Payload));
                    break;
                case MessageType.RoundResult:
                    OnRoundResult?.Invoke(Deserialize<RoundResultPayload>(env.Payload));
                    break;
                case MessageType.MatchEnd:
                    OnMatchEnd?.Invoke(Deserialize<MatchEndPayload>(env.Payload));
                    break;
                case MessageType.PowerUpActivated:
                    OnPowerUpActivated?.Invoke(Deserialize<PowerUpActivatedPayload>(env.Payload));
                    break;
                case MessageType.ReconnectSync:
                    OnReconnectSync?.Invoke(Deserialize<ReconnectSyncPayload>(env.Payload));
                    break;
                case MessageType.OpponentDisconnected:
                    OnOpponentDisconnected?.Invoke(env.SenderId);
                    break;
                case MessageType.OpponentReconnected:
                    OnOpponentReconnected?.Invoke(env.SenderId);
                    break;
                case MessageType.OpponentAbandoned:
                    OnOpponentAbandoned?.Invoke(env.SenderId);
                    break;
                case MessageType.Pong:
                    HandlePong(Deserialize<PongPayload>(env.Payload));
                    break;
            }
        }

        // ----------------------------------------------------------
        // Latência (ping/pong)
        // ----------------------------------------------------------

        private IEnumerator PingLoop()
        {
            while (IsConnected)
            {
                yield return new WaitForSeconds(PingIntervalSeconds);
                SendPing();
            }
        }

        private void HandlePong(PongPayload pong)
        {
            long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - pong.OriginalClientTimestampMs;
            _latencySamples.Add(rtt / 2);
            if (_latencySamples.Count > 10) _latencySamples.RemoveAt(0);
            var sorted = new List<long>(_latencySamples);
            sorted.Sort();
            AverageLatencyMs = sorted[sorted.Count / 2];
        }

        private void StartPingLoop()  => _pingCoroutine = StartCoroutine(PingLoop());
        private void StopPingLoop()
        {
            if (_pingCoroutine != null) StopCoroutine(_pingCoroutine);
            _pingCoroutine = null;
        }

        // ----------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------

        private NetworkEnvelope BuildEnvelope<T>(MessageType type, T payload) =>
            new NetworkEnvelope
            {
                Type       = type,
                Payload    = PlayFabSimpleJson.SerializeObject(payload),
                SentAtMs   = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                SenderId   = LocalPlayerId,
                SequenceId = ++_sequenceCounter
            };

        private T Deserialize<T>(string json) => PlayFabSimpleJson.DeserializeObject<T>(json);
    }
}
