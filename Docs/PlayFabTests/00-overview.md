# PlayFab Test Documentation

## Objetivo
Este pacote documenta todos os testes PlayFab atualmente implementados no projeto, com foco em operacao pratica, entendimento tecnico do fluxo e uso correto dos services.

## Escopo
Coberto neste pacote:
- Auth
- Title Data
- Deck and Card flow (integracao hibrida)
- Economy (CloudScript)
- Inventory
- Player Data
- Statistics e Leaderboard
- Store
- Matchmaking
- Achievement flow (integracao parcial)
- Guia de uso dos services
- Troubleshooting

## Mapa Rapido De Funcionalidades
1. Auth
Resumo: valida login com CustomId persistente, reset de ID local e cenario de login invalido.
Detalhes: [01-auth.md](01-auth.md)

2. Title Data
Resumo: valida leitura de chaves de configuracao e decks no Title Data.
Detalhes: [02-title-data.md](02-title-data.md)

3. Economy (CloudScript)
Resumo: valida adicionar, subtrair e consultar saldo de moeda virtual via CloudScript seguro.
Detalhes: [03-economy.md](03-economy.md)

4. Deck and Card Flow
Resumo: valida carga de decks por Title Data no DeckService e consumo local no loop de cartas.
Detalhes: [12-deck-and-card-flow.md](12-deck-and-card-flow.md)

5. Inventory
Resumo: valida carga de inventario e consumo de item por ItemInstanceId.
Detalhes: [04-inventory.md](04-inventory.md)

6. Player Data
Resumo: valida carga, persistencia e reset de profile do jogador.
Detalhes: [05-player-data.md](05-player-data.md)

7. Statistics e Leaderboard
Resumo: valida atualizacao de score/wins/losses e consulta do leaderboard.
Detalhes: [06-statistics.md](06-statistics.md)

8. Store
Resumo: valida carga de loja/catalogo e compra de item com moeda e preco corretos.
Detalhes: [07-store.md](07-store.md)

9. Matchmaking
Resumo: valida fluxo fim a fim com dois usuarios, tickets, polling e match compartilhado.
Detalhes: [08-matchmaking.md](08-matchmaking.md)

10. Achievement Flow
Resumo: valida regras locais de conquista apoiadas por updates de estatisticas.
Detalhes: [09-achievement-flow.md](09-achievement-flow.md)

11. Services
Resumo: padrao de arquitetura dos services e como integrar novos testers.
Detalhes: [10-services-usage.md](10-services-usage.md)

12. Troubleshooting
Resumo: tabela de erros recorrentes e diagnostico rapido.
Detalhes: [11-troubleshooting.md](11-troubleshooting.md)

## Ordem Recomendada De Leitura
1. [10-services-usage.md](10-services-usage.md)
2. [01-auth.md](01-auth.md)
3. [02-title-data.md](02-title-data.md)
4. [12-deck-and-card-flow.md](12-deck-and-card-flow.md)
5. [03-economy.md](03-economy.md)
6. [07-store.md](07-store.md)
7. [08-matchmaking.md](08-matchmaking.md)
8. [11-troubleshooting.md](11-troubleshooting.md)

## Fontes De Codigo
- Test harness base: [../../Assets/Scripts/Testing/PlayFabTerminalTester.cs](../../Assets/Scripts/Testing/PlayFabTerminalTester.cs)
- Services: [../../Assets/Scripts/Services](../../Assets/Scripts/Services)
- Testers: [../../Assets/Scripts/Testing](../../Assets/Scripts/Testing)
- CloudScript Economy: [../PlayFab/CloudScriptEconomy.js](../PlayFab/CloudScriptEconomy.js)
