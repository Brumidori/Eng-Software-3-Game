# Deck And Card Flow

## Escopo
Documenta o fluxo de decks e cartas que depende de Title Data via DeckService e e exercitado no [../../Assets/Scripts/CardTester.cs](../../Assets/Scripts/CardTester.cs).

## Classificacao
Integracao hibrida.

- Integrado ao PlayFab para carga de indice e decks (DeckService).
- Loop de quiz e local no CardTester (UI/logica de jogo local).

## Dependencias
- DeckService: [../../Assets/Scripts/Services/DeckService.cs](../../Assets/Scripts/Services/DeckService.cs)
- Modelos: [../../Assets/Scripts/Models/DeckIndex.cs](../../Assets/Scripts/Models/DeckIndex.cs), [../../Assets/Scripts/Models/Carta.cs](../../Assets/Scripts/Models/Carta.cs)
- Chaves de Title Data como `deck_index` e `cartas_<categoria>`

## Objetivo Tecnico
Validar:
- Carga do indice de categorias em Title Data.
- Carga de deck por categoria com cache local.
- Sorteio de carta aleatoria para gameplay local.

## Fluxo Do DeckService
1. Inicializa apos login.
2. Carrega `deck_index` com `GetTitleData`.
3. Resolve categoria para chave de deck.
4. Carrega deck por chave e desserializa `DeckWrapper`.
5. Armazena em cache para reuso.
6. Fornece `GetRandomCarta(categoria)` para consumo.

## Fluxo Do CardTester
1. Jogador abre menu e escolhe categoria.
2. CardTester aciona `DeckService.LoadDeck(categoria)`.
3. Seleciona carta aleatoria carregada.
4. Processa resposta e resultado localmente.

## Como Usar
1. Garantir login e DeckService inicializado.
2. Confirmar existencia de `deck_index` e chaves de categoria no Title Data.
3. Rodar CardTester e selecionar categoria.
4. Validar logs de carga e perguntas exibidas.

## Indicadores De Sucesso
- `DeckService` loga indice e deck carregados com contagem de cartas.
- CardTester exibe pergunta, alternativas e valida resposta.

## Observacoes
- Este fluxo nao e um tester PlayFab de backend completo na pasta Testing, mas usa integracao PlayFab real para obter conteudo.
- Falhas de chave em Title Data impactam diretamente a disponibilidade de categorias e cartas.
