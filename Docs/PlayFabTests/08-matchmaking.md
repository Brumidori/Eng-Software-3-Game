# Matchmaking Tests

## Escopo
Teste fim a fim de matchmaking 2x usuarios no harness [../../Assets/Scripts/Testing/MatchmakingTester.cs](../../Assets/Scripts/Testing/MatchmakingTester.cs) com orquestracao no [../../Assets/Scripts/Services/MatchmakingService.cs](../../Assets/Scripts/Services/MatchmakingService.cs).

## Dependencias
- MatchmakingService
- PlayFab ClientInstance API para login por usuario
- PlayFab MultiplayerInstance API para tickets e match

## Objetivo Tecnico
Validar:
- Login de dois usuarios com contexts separados.
- Criacao de tickets na mesma queue.
- Polling de status ate obter MatchId compartilhado.
- Consulta detalhada do match.
- Cancelamento limpo em timeout/cancelamento manual.

## Atalhos
- Tecla 1: iniciar teste 2 usuarios
- Tecla 2: cancelar busca
- Tecla 3: mostrar estado atual
- Tecla 4: mostrar diagnostico completo

## Configuracao Principal
No tester:
- `queueName`
- `userAId`
- `userBId`
- `timeoutSeconds`
- `pollIntervalSeconds`
- `createMissingUsersForTest`

Migracoes automaticas em `OnValidate`:
- queue legada `queue_test!` -> `queue_test`
- user B legado `test_user_456` -> `teste_user_456`

## Fluxo Detalhado
1. Inicio:
- Service recebe parametros e valida preenchimento.
- Estado vai para `Searching`.

2. Login por usuario:
- Cada usuario usa `PlayFabClientInstanceAPI` proprio.
- Opcionalmente, retry com `CreateAccount=true` quando `AccountNotFound` e flag de teste ativa.

3. Limpeza:
- `CancelAllMatchmakingTicketsForPlayer` por usuario para remover tickets residuais.

4. Criacao de tickets:
- `CreateMatchmakingTicket` com `Creator.Entity` e `QueueName`.

5. Polling:
- `GetMatchmakingTicket` para cada ticket em loop.
- Match valido quando ambos tem o mesmo `MatchId`.

6. Detalhamento:
- `GetMatch` para listar membros e dados de arranjo.

7. Encerramento:
- Timeout: cancela tickets ativos e marca `TimedOut`.
- Cancelamento manual: cancela tickets e marca `Cancelled`.
- Falha: marca `Failed` e emite evento de erro.

## Como Usar
1. Confirmar queue e users em runtime pelo log de config do tester.
2. Pressionar 1 para iniciar.
3. Acompanhar transicao de estado.
4. Pressionar 4 para diagnostico se necessario.
5. Pressionar 2 para abortar com limpeza.

## Indicadores De Sucesso
- Estado `Matched`.
- Log de MatchId compartilhado.
- Logs de membros retornados por `GetMatch`.

## Uso Do Service
Metodos:
- `StartTwoUserMatchmaking(queue, userAId, userBId, timeout, pollInterval, allowCreateMissingUsers=false)`
- `CancelCurrentSearch()`
- `GetDiagnosticsSummary()`

Eventos:
- `OnStateChanged`
- `OnMatchFound`
- `OnMatchmakingFailed`

## Observacoes
- `User not found` geralmente aponta para mismatch de TitleId, ID incorreto ou valor serializado antigo no Inspector.
- Mudancas de defaults no script nao sobrescrevem automaticamente valores serializados existentes no componente.
