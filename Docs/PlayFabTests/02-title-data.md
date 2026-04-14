# Title Data Tests

## Escopo
Teste de leitura de Title Data por chaves especificas no harness [../../Assets/Scripts/Testing/TitleDataTester.cs](../../Assets/Scripts/Testing/TitleDataTester.cs).

## Dependencias
- PlayFabService facade (`GetTitleData`)
- Chaves de title data configuradas no PlayFab

## Objetivo Tecnico
Validar leitura confiavel de dados estaticos de configuracao e conteudo:
- `deck_index`
- `cartas_esportes`
- chave customizada configurada no Inspector

## Atalhos
- Tecla 1: ler `deck_index`
- Tecla 2: ler chave customizada (`customKey`)
- Tecla 3: ler `cartas_esportes`
- Tecla 4: mostrar valor atual de `customKey`

## Fluxo Detalhado
1. Tester monta `GetTitleDataRequest` com lista de Keys.
2. Service facade executa request no endpoint client.
3. Callback valida existencia da chave no dicionario retornado.
4. Valor JSON/string e impresso em log.

## Como Usar
1. Definir `customKey` se necessario.
2. Rodar cena de testes.
3. Pressionar 1, 2 e 3 para validar retorno por chave.
4. Verificar warnings de chave ausente para detectar configuracao incompleta no title.

## Indicadores De Sucesso
- Log `✅ {key}: {value}` para chaves existentes.
- Warning claro para chaves ausentes, sem excecao.

## Uso Do Service
- O tester usa PlayFabService.Client diretamente para GetTitleData.
- Nao ha cache local neste tester; objetivo e inspecao rapida e valida.

## Observacoes
- Este tester e util para diagnosticar erros de decks e configuracoes que dependem de Title Data.
- Para consumo estruturado de decks com cache, usar DeckService.
