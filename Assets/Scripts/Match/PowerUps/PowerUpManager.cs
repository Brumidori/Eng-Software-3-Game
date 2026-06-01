// ============================================================
// PowerUpManager.cs — gerencia o inventário de power-ups do
// jogador local e expõe ações de ativação para a UI.
//
// Regras:
//  - Máximo 1 power-up por partida (20 rodadas)
//  - Só pode ativar durante a fase ThemeAndPowerUp (4 s)
//  - Power-up é carregado do deck do jogador antes da partida
// ============================================================
using System;
using UnityEngine;
using BrainDuel.Match;
using BrainDuel.Match.Core;

namespace BrainDuel.Match.PowerUps
{
    public class PowerUpManager : MonoBehaviour
    {
        // ----------------------------------------------------------
        // Eventos para a UI
        // ----------------------------------------------------------
        public event Action<PowerUpType> OnPowerUpActivated;
        public event Action              OnPowerUpUnavailable;

        // ----------------------------------------------------------
        // Estado
        // ----------------------------------------------------------
        public PowerUpType EquippedPowerUp { get; private set; }
        public bool        IsUsed          { get; private set; }
        public bool        CanActivate     => !IsUsed && EquippedPowerUp != PowerUpType.None;

        private MatchContext      _context;
        private MatchStateMachine _machine;

        // ----------------------------------------------------------
        // Inicialização
        // ----------------------------------------------------------

        public void Initialize(MatchContext context, MatchStateMachine machine, PowerUpType equippedPowerUp)
        {
            _context      = context;
            _machine      = machine;
            EquippedPowerUp = equippedPowerUp;
            IsUsed        = false;
        }

        // ----------------------------------------------------------
        // Ativação (chamado pela UI durante ThemeAndPowerUp)
        // ----------------------------------------------------------

        public void TryActivate() => TryActivate(EquippedPowerUp);

        public void TryActivate(PowerUpType tipo)
        {
            if (IsUsed)
            {
                OnPowerUpUnavailable?.Invoke();
                return;
            }

            if (tipo == PowerUpType.None)
            {
                OnPowerUpUnavailable?.Invoke();
                return;
            }

            if (_machine.Phase != MatchPhase.ThemeAndPowerUp)
            {
                Debug.LogWarning("[PowerUp] Fora do período de ativação");
                OnPowerUpUnavailable?.Invoke();
                return;
            }

            IsUsed          = true;
            EquippedPowerUp = tipo;
            _machine.ActivatePowerUp(tipo);
            OnPowerUpActivated?.Invoke(tipo);

            Debug.Log($"[PowerUp] {tipo} ativado na rodada {_context?.CurrentRound}");
        }

        // ----------------------------------------------------------
        // Descrições (para tooltip na UI)
        // ----------------------------------------------------------

        public static string GetName(PowerUpType type) => type switch
        {
            PowerUpType.SimpleShield => "Escudo Simples",
            PowerUpType.DoubleShield => "Escudo Duplo",
            PowerUpType.EliminateTwo => "Eliminar 2",
            PowerUpType.Bet          => "Aposta",
            PowerUpType.Steal        => "Roubo",
            _                        => "Nenhum"
        };

        public static string GetDescription(PowerUpType type) => type switch
        {
            PowerUpType.SimpleShield => "Bloqueia o dano desta rodada.",
            PowerUpType.DoubleShield => "Bloqueia os próximos 2 danos da partida.",
            PowerUpType.EliminateTwo => "Remove 2 respostas erradas.",
            PowerUpType.Bet          => "+5 de dano se acertar a pergunta.",
            PowerUpType.Steal        => "Rouba 5 HP do inimigo, independente da resposta.",
            _                        => string.Empty
        };
    }
}
