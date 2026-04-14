# Inventory Tests

## Escopo
Teste de inventario e consumo de item no harness [../../Assets/Scripts/Testing/InventoryTester.cs](../../Assets/Scripts/Testing/InventoryTester.cs).

## Dependencias
- InventoryService: [../../Assets/Scripts/Services/InventoryService.cs](../../Assets/Scripts/Services/InventoryService.cs)
- PlayFabService autenticado

## Objetivo Tecnico
Validar:
- Carga de inventario do jogador logado.
- Consumo de item por ItemInstanceId.
- Atualizacao de estado local de cache para inspeção.

## Atalhos
- Tecla 1: carregar inventario
- Tecla 2: consumir item configurado (`fallbackItemInstanceId`)
- Tecla 3: consumir primeiro item do cache
- Tecla 4: mostrar total de itens em cache

## Fluxo Detalhado
1. Tester chama `LoadInventory`.
2. Service executa `GetUserInventory`.
3. Resultado e propagado por `OnInventoryLoaded(List<ItemInstance>)`.
4. Tester armazena copia em cache para operacoes seguintes.
5. Consumo chama `ConsumeItem` com `ConsumeCount=1`.
6. Service publica `OnItemConsumed(itemInstanceId)`.

## Como Usar
1. Garantir jogador com inventario no PlayFab.
2. Pressionar 1 para carregar itens.
3. Validar InstanceId no log.
4. Consumir com 2 (item especifico) ou 3 (primeiro do cache).

## Indicadores De Sucesso
- Carga: `✅ Inventario carregado com N itens`.
- Consumo: `✅ Item consumido: {instanceId}`.

## Uso Do Service
Metodos:
- `LoadInventory()`
- `ConsumeItem(itemInstanceId, consumeCount=1)`

Eventos:
- `OnInventoryLoaded`
- `OnItemConsumed`
- `OnInventoryFailed`

## Observacoes
- `ItemInstanceId` vazio e bloqueado por validacao.
- Sempre recarregar inventario ao depurar divergencias de estado.
