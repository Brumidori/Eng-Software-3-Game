# Statistics And Leaderboard Tests

## Escopo
Teste de estatisticas e ranking no harness [../../Assets/Scripts/Testing/StatisticsTester.cs](../../Assets/Scripts/Testing/StatisticsTester.cs).

## Dependencias
- StatisticsService: [../../Assets/Scripts/Services/StatisticsService.cs](../../Assets/Scripts/Services/StatisticsService.cs)
- Modelo: [../../Assets/Scripts/Models/LeaderboardEntryData.cs](../../Assets/Scripts/Models/LeaderboardEntryData.cs)
- PlayFabService autenticado

## Objetivo Tecnico
Validar:
- Atualizacao de score.
- Contadores de wins e losses.
- Consulta de leaderboard por score.

## Atalhos
- Tecla 1: registrar vitoria (`winScore`, wins +1)
- Tecla 2: registrar derrota (`lossScore`, losses +1)
- Tecla 3: consultar leaderboard (`leaderboardSize`)
- Tecla 4: enviar pontuacao customizada (250)

## Fluxo Detalhado
1. Tester dispara `UpdateMatchStatistics(scoreDelta, wonMatch)`.
2. Service monta dois `StatisticUpdate`:
- `score`
- `wins` ou `losses` com valor 1
3. Service envia `UpdatePlayerStatistics`.
4. Para ranking, service chama `GetLeaderboard` em `score`.
5. Resultado e mapeado para `LeaderboardEntryData` e publicado por evento.

## Como Usar
1. Pressionar 1 e 2 para gerar historico de estatisticas.
2. Pressionar 3 para visualizar ranking.
3. Repetir com mais contas para validar ordenacao relativa.

## Indicadores De Sucesso
- `✅ Estatisticas atualizadas com sucesso`.
- `✅ Leaderboard recebido com N entradas` com linhas `#pos jogador => valor`.

## Uso Do Service
Metodos:
- `UpdateMatchStatistics(scoreDelta, wonMatch)`
- `GetLeaderboard(maxResults=10)`

Eventos:
- `OnStatisticsUpdated`
- `OnLeaderboardLoaded`
- `OnStatisticsFailed`

## Observacoes
- O leaderboard esta baseado na estatistica `score`.
- `wins` e `losses` sao mantidos para analise adicional, mas nao definem o ranking principal nesse fluxo.
