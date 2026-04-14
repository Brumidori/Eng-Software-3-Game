# Services Usage Guide

## Arquitetura Geral
O projeto usa um padrao consistente:
- Tester (input por teclado) dispara operacoes.
- Service encapsula regra de negocio, validacao e chamadas PlayFab.
- Eventos estaticos sinalizam sucesso e falha para o tester.
- PlayFabService centraliza bootstrap e login.

Fluxo padrao:
Tester -> Service -> PlayFab API -> callback -> evento -> log no tester

## Base Dos Testers
Arquivo: [../../Assets/Scripts/Testing/PlayFabTerminalTester.cs](../../Assets/Scripts/Testing/PlayFabTerminalTester.cs)

Responsabilidades:
- Bootstrapping automatico do PlayFabService quando `autoBootstrapPlayFab` esta ativo.
- Garantia de instancia de service com `EnsureService<T>()`.
- Utilitarios de teclado e mensagem pronta.

## Service Central De Login
Arquivo: [../../Assets/Scripts/Services/PlayFabService.cs](../../Assets/Scripts/Services/PlayFabService.cs)

Pontos principais:
- Singleton global.
- Define TitleId com PlayFabConfig e executa LoginWithCustomId.
- Exponibiliza `IsLoggedIn()` para validacao nos demais services.
- Eventos globais: `OnLoginSuccess`, `OnLoginFailure`.

## Facade De API
Arquivos:
- [../../Assets/Scripts/Services/IPlayFabClientFacade.cs](../../Assets/Scripts/Services/IPlayFabClientFacade.cs)
- [../../Assets/Scripts/Services/PlayFabClientFacade.cs](../../Assets/Scripts/Services/PlayFabClientFacade.cs)

Papel:
- Isolar chamadas diretas ao SDK PlayFab.
- Facilitar manutencao e testes futuros com ponto unico de integracao.

## Contrato De Um Service Bem Comportado
1. Validar autenticacao antes de chamar API.
2. Validar entrada (exemplo: quantidade > 0, itemId preenchido).
3. Encapsular request PlayFab no service.
4. Expor eventos de sucesso/falha consumidos pelo tester.
5. Emitir logs objetivos com contexto de request e erro.

## Como Integrar Um Novo Tester
1. Herdar de PlayFabTerminalTester.
2. Em `Start()`, chamar `base.Start()`.
3. Garantir o service com `EnsureService<SeuService>()`.
4. Inscrever handlers de eventos do service.
5. Em `OnDestroy()`, desinscrever eventos.
6. Em `Update()`, mapear teclas e chamar metodos publicos do service.

## Mapa Tester Para Service
- AuthTester -> PlayFabService (e PlayFabConfig)
- TitleDataTester -> PlayFabService.Client
- EconomyTester -> EconomyService
- InventoryTester -> InventoryService
- PlayerDataTester -> PlayerDataService
- StatisticsTester -> StatisticsService
- StoreTester -> StoreService
- MatchmakingTester -> MatchmakingService
- AchievementTester -> StatisticsService + PlayerDataService

## Validacoes Importantes Em Runtime
- Sempre confirmar TitleId ativo no log do inicio de sessao.
- Para valores serializados no Inspector, considerar migracoes no `OnValidate` quando necessario.
- Em features de loja e matchmaking, preferir logs com IDs efetivos em runtime (item, queue, user, moeda).

## Dependencias Configuracionais
Arquivo: [../../Assets/Scripts/Config/PlayFabConfig.cs](../../Assets/Scripts/Config/PlayFabConfig.cs)

Campos de maior impacto:
- `CurrentEnv`
- `GetTitleId()`
- `GetCreateAccountFlag()`
- `GetTestUserId()`

## Boas Praticas Para Evolucao
- Nao colocar regra de negocio complexa no tester.
- Colocar o comportamento de integracao e fallback no service.
- Reusar eventos para manter desacoplamento.
- Documentar defaults de Inspector que impactam testes.
