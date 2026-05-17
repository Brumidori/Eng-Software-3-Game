// ============================================================
// DamageCalculator.cs — lógica de dano ESPELHADA no cliente.
// A versão autoritativa está em CloudScript/ServerDamageCalc.cs.
// O cliente usa esta classe apenas para exibir preview antes
// do resultado oficial chegar do servidor.
// ============================================================
using BrainDuel.Match;

namespace BrainDuel.Match.Core
{
    public static class DamageCalculator
    {
        // ----------------------------------------------------------
        // Ponto de entrada principal
        // ----------------------------------------------------------

        public static (RoundPlayerResult p1, RoundPlayerResult p2) Calculate(
            RoundPlayerAction p1Action, PlayerMatchState p1State,
            RoundPlayerAction p2Action, PlayerMatchState p2State,
            string correctAnswerId)
        {
            var p1Result = BuildResult(p1Action, p1State);
            var p2Result = BuildResult(p2Action, p2State);

            ApplyDamage(p1Result, p1Action, p1State, p2Result, p2Action, p2State, correctAnswerId);
            ApplyDamage(p2Result, p2Action, p2State, p1Result, p1Action, p1State, correctAnswerId);

            // Clamp HP
            p1Result.HPAfter = System.Math.Max(0, p1State.HP - p1Result.DamageDealt + p1Result.Breakdown.StolenHP);
            p2Result.HPAfter = System.Math.Max(0, p2State.HP - p2Result.DamageDealt + p2Result.Breakdown.StolenHP);

            // Steal rouba HP do oponente
            p1Result.HPAfter -= p2Result.Breakdown.StolenHP;
            p2Result.HPAfter -= p1Result.Breakdown.StolenHP;

            p1Result.HPAfter = System.Math.Max(0, p1Result.HPAfter);
            p2Result.HPAfter = System.Math.Max(0, p2Result.HPAfter);

            return (p1Result, p2Result);
        }

        // ----------------------------------------------------------
        // Calcula dano que um jogador causa no outro
        // ----------------------------------------------------------

        private static void ApplyDamage(
            RoundPlayerResult attackerResult, RoundPlayerAction attackerAction, PlayerMatchState attackerState,
            RoundPlayerResult defenderResult, RoundPlayerAction defenderAction, PlayerMatchState defenderState,
            string correctAnswerId)
        {
            var bd = attackerResult.Breakdown;

            // --- Não respondeu: perde HP próprio ---
            if (!attackerAction.HasAnswered)
            {
                bd.SelfDamage = DamageConfig.SelfDamageNoAnswer;
                attackerResult.Result = AnswerResult.NotAnswered;
                attackerResult.StreakAfter = 0;
                return;
            }

            bool correct = attackerAction.AnswerId == correctAnswerId;
            attackerResult.Result = correct ? AnswerResult.Correct : AnswerResult.Incorrect;

            // --- Steal: rouba HP sempre que ativado ---
            if (attackerAction.ActivatedPowerUp == PowerUpType.Steal)
            {
                bd.StolenHP = DamageConfig.StealAmount;
            }

            if (!correct)
            {
                attackerResult.StreakAfter = 0;
                return; // errou: só Steal conta
            }

            // --- Acertou ---
            int newStreak = attackerState.Streak + 1;
            attackerResult.StreakAfter = newStreak;

            bd.BaseDamage  = DamageConfig.BaseDamage;
            bd.StreakBonus = DamageConfig.GetStreakBonus(newStreak);

            // Bônus de velocidade: ambos acertaram e diferença > 200ms
            if (defenderAction.HasAnswered && defenderAction.AnswerId == correctAnswerId)
            {
                long diff = System.Math.Abs(attackerAction.AnswerTimestampMs - defenderAction.AnswerTimestampMs);
                if (diff > MatchConfig.SpeedBonusThresholdMs &&
                    attackerAction.AnswerTimestampMs < defenderAction.AnswerTimestampMs)
                {
                    bd.SpeedBonus = DamageConfig.SpeedBonus;
                }
            }

            // Power-up Bet: +5 se acertou
            if (attackerAction.ActivatedPowerUp == PowerUpType.Bet)
                bd.PowerUpBonus = DamageConfig.BetBonus;

            // Shield: neutraliza dano no defensor
            bool shielded = IsShielded(defenderState, defenderAction);
            if (shielded)
            {
                defenderResult.WasShielded = true;
                bd.BaseDamage  = 0;
                bd.SpeedBonus  = 0;
                bd.StreakBonus = 0;
                bd.PowerUpBonus = 0;
            }

            attackerResult.DamageDealt = bd.Total;
        }

        // ----------------------------------------------------------
        // Helpers
        // ----------------------------------------------------------

        private static RoundPlayerResult BuildResult(RoundPlayerAction action, PlayerMatchState state) =>
            new RoundPlayerResult
            {
                PlayerId   = action.PlayerId,
                AnsweredId = action.AnswerId,
                HPBefore   = state.HP,
                Breakdown  = new DamageBreakdown()
            };

        private static bool IsShielded(PlayerMatchState state, RoundPlayerAction action)
        {
            if (action.ActivatedPowerUp == PowerUpType.SimpleShield) return true;
            if (action.ActivatedPowerUp == PowerUpType.DoubleShield) return true;
            if (state.DoubleShieldCharges > 0) return true;
            return false;
        }

        // ----------------------------------------------------------
        // Após processar: atualiza o estado dos power-ups no state
        // (chamado pelo servidor após calcular resultado autoritativo)
        // ----------------------------------------------------------

        public static void UpdatePowerUpCharges(PlayerMatchState state, RoundPlayerAction action, bool wasShielded)
        {
            if (wasShielded && state.DoubleShieldCharges > 0)
                state.DoubleShieldCharges--;

            if (action.ActivatedPowerUp == PowerUpType.DoubleShield)
            {
                // Primeira carga consumida agora; a segunda fica salva
                state.DoubleShieldCharges = 1;
                state.HasUsedPowerUp = true;
            }
            else if (action.ActivatedPowerUp != PowerUpType.None)
            {
                state.HasUsedPowerUp = true;
            }
        }
    }
}
