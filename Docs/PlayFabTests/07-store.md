# Store Tests

## Escopo
Teste de loja/catalogo e compra no harness [../../Assets/Scripts/Testing/StoreTester.cs](../../Assets/Scripts/Testing/StoreTester.cs).

## Dependencias
- StoreService: [../../Assets/Scripts/Services/StoreService.cs](../../Assets/Scripts/Services/StoreService.cs)
- Modelo: [../../Assets/Scripts/Models/StoreItemData.cs](../../Assets/Scripts/Models/StoreItemData.cs)
- PlayFabService autenticado

## Objetivo Tecnico
Validar:
- Carga de itens por StoreId (`GetStoreItems`) ou catalogo (`GetCatalogItems`).
- Compra de item com combinacao correta de ItemId, moeda, preco, catalog/store context.
- Diagnostico de erros de moeda/preco.

## Atalhos
- Tecla 1: carregar loja
- Tecla 2: comprar item configurado
- Tecla 3: comprar primeiro item carregado
- Tecla 4: mostrar total de itens em cache

## Campos Relevantes
- `fallbackStoreId` (default `loja_teste`)
- `fallbackItemId` (default `One`)
- `fallbackCurrency` (default `DA`)
- `fallbackPrice`
- `fallbackCatalogVersion`

## Fluxo Detalhado
1. Carga da loja:
- Tester chama `LoadCatalog(catalogVersion, storeId)`.
- Service usa `GetStoreItems` quando StoreId esta presente.
- Resultado e convertido para `StoreItemData` em cache.

2. Compra configurada:
- Tester tenta achar item no cache por `fallbackItemId`.
- Se encontrou, usa moeda e preco reais da loja carregada.
- Se nao encontrou, cai no fallback configurado e avisa no log.
- Service envia `PurchaseItem` com StoreId.

3. Compra do primeiro item:
- Usa item do indice 0 do cache e envia request.

## Como Usar
1. Pressionar 1 para carregar loja primeiro.
2. Confirmar itens e preco/moeda nos logs.
3. Pressionar 2 para compra do item alvo.
4. Usar 3 para validar compra generica do primeiro item.

## Indicadores De Sucesso
- `✅ Loja carregada com N itens`.
- `✅ Compra concluida com N item(ns)`.

## Uso Do Service
Metodos:
- `LoadCatalog(catalogVersion=null, storeId=null)`
- `PurchaseItem(itemId, virtualCurrency, price, catalogVersion=null, storeId=null)`

Eventos:
- `OnCatalogLoaded`
- `OnPurchaseCompleted`
- `OnStoreFailed`

## Observacoes
- Erro `WrongVirtualCurrency` costuma indicar mismatch entre moeda/preco enviados e configuracao real do item na store.
- Sempre preferir dados carregados da loja para preencher compra.
