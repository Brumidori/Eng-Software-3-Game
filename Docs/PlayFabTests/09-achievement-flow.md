# Achievement Flow Tests

## Escopo
Teste de regras de conquista no harness [../../Assets/Scripts/Testing/AchievementTester.cs](../../Assets/Scripts/Testing/AchievementTester.cs).

## Classificacao De Integracao
Integracao parcial.

Motivo:
- Atualiza estatisticas via StatisticsService (integrado ao PlayFab).
- Avaliacao de conquista e feita localmente no client, sem API dedicada de achievement no backend.

## Dependencias
- StatisticsService
- PlayerDataService

## Objetivo Tecnico
Validar regra composta local:
- wins >= requiredWins
- score >= requiredScore
- level >= requiredLevel

## Atalhos
- Tecla 1: simular vitoria (wins +1, score +100)
- Tecla 2: simular derrota (score +10)
- Tecla 3: avaliar desbloqueio
- Tecla 4: exibir progresso local

## Campos Relevantes
- `requiredWins`
- `requiredScore`
- `requiredLevel`

## Fluxo Detalhado
1. Tester inicia com dependencias de StatisticsService e PlayerDataService.
2. Simulacao de resultado altera contadores locais (`localWins`, `localScore`).
3. Em paralelo, envia update de estatistica para PlayFab.
4. Avaliacao de conquista compara valores locais com thresholds configurados.
5. Resultado e logado como desbloqueada ou bloqueada.

## Como Usar
1. Definir thresholds no Inspector.
2. Simular partidas com teclas 1 e 2.
3. Pressionar 3 para checar desbloqueio.
4. Usar tecla 4 para depurar progresso atual.

## Indicadores De Sucesso
- Logs coerentes de progresso local.
- Update de estatistica sendo aceito em paralelo.

## Uso Dos Services
- StatisticsService: `UpdateMatchStatistics` para rastrear resultado.
- PlayerDataService: leitura de `CurrentProfile` para obter level.

## Limitacoes Atuais
- Sem persistencia formal de conquista desbloqueada no backend.
- Sem endpoint PlayFab dedicado de achievements neste fluxo.

## Recomendacao De Evolucao
- Criar service de achievements persistidos (UserData, Statistics thresholds server-side ou CloudScript).
- Mover regra de avaliacao critica para backend para evitar divergencia client/server.
