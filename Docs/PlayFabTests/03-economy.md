# Economy Tests (CloudScript)

## Escopo
Teste de operacoes de moeda virtual via EconomyService no harness [../../Assets/Scripts/Testing/EconomyTester.cs](../../Assets/Scripts/Testing/EconomyTester.cs).

## Dependencias
- EconomyService: [../../Assets/Scripts/Services/EconomyService.cs](../../Assets/Scripts/Services/EconomyService.cs)
- CloudScript: [../PlayFab/CloudScriptEconomy.js](../PlayFab/CloudScriptEconomy.js)
- PlayFabService autenticado

## Objetivo Tecnico
Validar operacoes de economia protegidas por servidor:
- Adicao de moeda
- Subtracao com verificacao de saldo
- Consulta de saldo

## Atalhos
- Tecla 1: adicionar moeda (`addAmount`)
- Tecla 2: remover moeda (`subtractAmount`)
- Tecla 3: consultar saldo
- Tecla 4: adicionar saldo alto (1000)

## Configuracao Principal
No tester:
- `currencyCode` (default DA)
- `addAmount`
- `subtractAmount`

No service:
- `addCurrencyFunction`
- `subtractCurrencyFunction`
- `getBalanceFunction`
- `useLatestRevision`
- `generatePlayStreamEvent`

## Fluxo Detalhado
1. Tester chama metodo publico do EconomyService.
2. Service valida auth e parametros (moeda preenchida, amount > 0).
3. Service monta `ExecuteCloudScriptRequest`.
4. CloudScript executa `server.AddUserVirtualCurrency`, `server.SubtractUserVirtualCurrency` ou leitura de inventario.
5. Service normaliza payload e extrai `balance`.
6. Event `OnCurrencyChanged(currency, balance)` notifica tester.

## Como Usar
1. Publicar funcoes do arquivo CloudScript no PlayFab do Title correto.
2. Rodar cena com EconomyTester.
3. Pressionar 1 e 2 para mutacoes.
4. Pressionar 3 para confirmar saldo final.

## Indicadores De Sucesso
- Logs `✅ Saldo de {currency}: {balance}` no tester.
- Logs do service com nome da funcao, TitleId e revision.

## Uso Do Service
Metodos publicos:
- `AddCurrency(virtualCurrencyCode, amount)`
- `SubtractCurrency(virtualCurrencyCode, amount)`
- `GetBalance(virtualCurrencyCode)`

Eventos:
- `OnCurrencyChanged`
- `OnEconomyFailed`

## Observacoes
- A logica sensivel de economia fica no servidor (CloudScript), reduzindo risco de fraude no client.
- Erros como `No function named` indicam deploy ausente ou nome divergente.
