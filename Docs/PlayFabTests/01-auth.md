# Auth Tests

## Escopo
Teste de autenticacao por CustomId usando o harness [../../Assets/Scripts/Testing/AuthTester.cs](../../Assets/Scripts/Testing/AuthTester.cs).

## Dependencias
- PlayFabService: [../../Assets/Scripts/Services/PlayFabService.cs](../../Assets/Scripts/Services/PlayFabService.cs)
- PlayFabConfig: [../../Assets/Scripts/Config/PlayFabConfig.cs](../../Assets/Scripts/Config/PlayFabConfig.cs)

## Objetivo Tecnico
Validar:
- Login persistente com CreateAccount=true.
- Reset de ID local para cenarios de teste controlado.
- Comportamento esperado de erro com credencial invalida (CreateAccount=false).

## Atalhos
- Tecla 1: login persistente
- Tecla 2: resetar ID local e relogar
- Tecla 3: login invalido esperado
- Tecla 4: exibir CustomId atual em memoria

## Fluxo Detalhado
1. O tester herda de PlayFabTerminalTester e inicia bootstrap automatico.
2. O login persistente usa `PlayFabConfig.GetTestUserId()`.
3. O request de login e enviado por `PlayFabService.Client.LoginWithCustomID`.
4. O resultado e registrado em log com PlayFabId no sucesso.
5. O teste invalido usa CustomId randomico com CreateAccount=false para provocar erro esperado.

## Como Usar
1. Abrir cena com AuthTester ativo.
2. Pressionar 1 para validar login nominal.
3. Pressionar 4 para confirmar ID atual.
4. Pressionar 3 para validar tratamento de erro.
5. Pressionar 2 para resetar persistencia local e repetir.

## Indicadores De Sucesso
- Logs de sucesso com PlayFabId em login persistente.
- Erro esperado em login invalido sem crash nem estado inconsistente.

## Uso Do Service
- O tester chama a facade do PlayFab via PlayFabService.Client.
- O service central controla TitleId e estado de login para os demais modulos.

## Observacoes
- Como CreateAccount=true no fluxo principal, o primeiro login pode criar conta automaticamente.
- Em mudanca de ambiente, confirmar TitleId para nao confundir contas entre titles.
