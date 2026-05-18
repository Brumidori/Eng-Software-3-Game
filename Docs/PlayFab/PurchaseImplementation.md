# Implementação: Compra Segura de Items na Store

## Resumo das Mudanças

Esta implementação adiciona um fluxo seguro e controlado para comprar items na loja, com validação server-side, modal de confirmação e feedback visual.

---

## Arquivos Modificados / Criados

### 1. **CloudScript Handler** (`Docs/PlayFab/CloudScriptEconomy.js`)
- ✅ Adicionado handler `PurchaseItemSecure()`
- Valida saldo suficiente e existência do item
- Subtrai moeda virtual de forma segura no servidor
- Retorna novo saldo e confirmação

### 2. **StoreService** (`Assets/Scripts/Services/StoreService.cs`)
- ✅ Adicionada classe `PurchaseResult` para encapsular resultado
- ✅ Novo método `PurchaseItemViaCloudScript(StoreItemData item, Action<PurchaseResult>)`
- ✅ Novos eventos: `OnPurchaseCompletedSecure`, `OnPurchaseFailed`
- ✅ Handlers para sucesso/erro do CloudScript com parsing de resposta
- ✅ Classe helper `PurchaseResultJson` para desserialização

### 3. **PurchaseConfirmationPanel** (`Assets/Scripts/UI/PurchaseConfirmationPanel.cs`) - NOVO
- ✅ Modal de confirmação antes de comprar
- ✅ Exibe: nome item, preço, saldo atual
- ✅ Validação de saldo na UI (desabilita botão se insuficiente)
- ✅ Suporte para ESC ou botão cancelar
- ✅ Feedback de erro se compra falhar

### 4. **Store Item Binder** (`Assets/Scripts/UI/StoreItemBinder.cs`)
- ✅ Nome, preço e botão ligados dinamicamente por item
- ✅ Ícone carregado dinamicamente por `iconUrl` ou `iconKey`
- ✅ Suporta sprite local em `Resources/StoreIcons/<id>` ou imagem remota

### 5. **PurchaseProgressManager** (`Assets/Scripts/UI/PurchaseProgressManager.cs`) - NOVO
- ✅ Singleton para gerenciar loading durante compra
- ✅ Spinner rotativo + timer
- ✅ Timeout warning (30s padrão)
- ✅ Bloqueia interações enquanto compra está em andamento
- ✅ Mostra mensagens de sucesso/erro

### 6. **StoreUIController** (`Assets/Scripts/UI/StoreUIController.cs`)
- ✅ Usa um único template de card já existente na Scene
- ✅ Clona o template para cada item carregado do catálogo
- ✅ Integração com `PurchaseConfirmationPanel`
- ✅ Método `OnBuyRequested()` adaptado para mostrar modal
- ✅ Nova lógica: modal → confirmado → CloudScript → atualiza saldo
- ✅ Fallback para API Client se modal não estiver configurado
- ✅ Listeners para eventos de compra

---

## Setup no Unity Editor

### Passo 1: Adicionar Managers à Cena
1. Criar um Canvas ou usar existente
2. Criar GameObject vazio: **"StoreManagers"**
3. Adicionar componentes:
   - `StoreService` (já deve estar singleton)
   - `PurchaseProgressManager` (novo singleton)
   - `PlayFabService` (já deve estar)

### Passo 2: Configurar PurchaseProgressManager
1. Selecionar o GameObject com `PurchaseProgressManager`
2. No Inspector:
   - **Loading Panel**: Arrastar um CanvasGroup (criar se não existir)
   - **Spinner Image**: Arrastar imagem de loading
   - **Progress Text**: Arrastar um Text para status
   - **Purchase Timeout Seconds**: 30 (padrão)

### Passo 3: Criar UI do Modal
1. No Canvas, criar um novo Panel: **"PurchaseConfirmationModal"**
   - Adicionar CanvasGroup
   - Adicionar `PurchaseConfirmationPanel` script
2. Dentro do Modal, adicionar UI Elements:
   ```
   PurchaseConfirmationModal/
   ├── ItemNameText (Text)
   ├── ItemDescriptionText (Text)
   ├── PriceText (Text)
   ├── CurrentBalanceText (Text)
   ├── WarningText (Text)
   ├── ConfirmButton (Button)
   └── CancelButton (Button)
   ```
3. Configurar `PurchaseConfirmationPanel` references no Inspector

### Passo 4: Preparar o Card Template da Store
1. Criar um único card visual na Scene e deixá-lo como template
2. Esse card deve conter:
    - `Image` para o ícone
    - `Text` para nome
    - `Text` para preço
    - `Button` de compra
3. Adicionar `StoreItemBinder` nesse objeto
4. Marcar esse template como desativado/oculto para não aparecer duplicado na tela

### Passo 5: Configurar StoreUIController
1. Selecionar SceneObject com `StoreUIController`
2. No Inspector, arrastar:
    - **Purchase Confirmation Panel**: Referência ao script criado no Passo 3
    - **Scene Item Template**: O card template da Scene criado no Passo 4
    - Já deve ter: itemsRoot, decksRoot

---

## Fluxo de Compra

```
[Card Button] 
    ↓
[OnBuyRequested] 
    ↓
[PurchaseConfirmationPanel.Show()]
    ↓
Usuario confirma? 
    ├─ NÃO → Modal fecha
    └─ SIM ↓
        [PurchaseProgressManager.StartPurchaseProgress()]
            ↓
        [StoreService.PurchaseItemViaCloudScript()]
            ↓
        [CloudScript: PurchaseItemSecure]
            - Valida saldo
            - Valida item existe
            - Subtrai moeda
            - Retorna novo saldo
            ↓
        ✅ Sucesso: Atualiza UI + Saldo
        ❌ Erro: Mostra mensagem específica
```

---

## Mensagens de Erro Tratadas

| Erro | Mensagem Amigável |
|------|-------------------|
| `insufficient_balance` | "Saldo insuficiente" |
| `item_not_found` | "Item não encontrado" |
| CloudScript erro | "Resposta inválida do servidor" |
| Network timeout | "Timeout ao processar compra" |
| Not authenticated | "Sessão PlayFab não autenticada" |

---

## Testando

### Teste Unitário (StoreTester)
```csharp
// Adicionar botão de teste:
if (Input.GetKeyDown(KeyCode.P))
{
    var testItem = new StoreItemData {
        itemId = "TestItem",
        displayName = "Item de Teste",
        price = 100,
        virtualCurrency = "DA",
        storeId = "test_store"
    };
    
    StoreUIController.Instance.OnBuyRequested(testItem); // Se público
}
```

### Ícones Dinâmicos
- Se o item tiver `ItemImageUrl` no catálogo PlayFab, o ícone é baixado automaticamente
- Se não houver URL, o binder tenta carregar `Resources/StoreIcons/<iconKey>`
- Por padrão, `iconKey` é preenchido com o `itemId`

### Teste Manual
1. Abrir scene Store
2. Fazer login PlayFab
3. Carregar catálogo (deve carregar items)
4. Clicar em um item → Modal aparece
5. Confirmar → Loading spinner
6. Verificar:
   - ✅ Saldo inicial vs final
   - ✅ Mensagem de sucesso/erro
   - ✅ Modal fecha após compra

---

## Validações Implementadas

### No CloudScript (Server):
- ✅ itemId obrigatório
- ✅ virtualCurrency obrigatório
- ✅ price > 0
- ✅ Saldo suficiente (compara balance atual)
- ✅ Item existe no catálogo

### Na UI (Client):
- ✅ Botão confirmação desabilitado se saldo insuficiente
- ✅ Modal pode ser cancelado (ESC ou botão)
- ✅ Loading timeout após 30s
- ✅ Feedback de erro com mensagem específica

---

## Events Disponíveis para Subscrição

```csharp
// Compra realizada com sucesso
StoreService.OnPurchaseCompletedSecure += (result) => {
    Debug.Log($"Compra OK: {result.ItemId}, Novo saldo: {result.NewBalance}");
};

// Compra falhou
StoreService.OnPurchaseFailed += (errorMsg) => {
    Debug.Log($"Erro: {errorMsg}");
};

// Compatibilidade: API Client (obsoleto)
StoreService.OnPurchaseCompleted += (result) => {
    // Usar OnPurchaseCompletedSecure para novo flow
};
```

---

## Arquitetura de Segurança

1. **Server-Authoritative**: CloudScript valida tudo no servidor
2. **Validação de Saldo**: Evita double-spend via timestamp+PlayFab internals
3. **Item Validation**: Verifica item existe antes de debitar
4. **Atomic Operations**: PlayFab garante atomicidade de SubtractUserVirtualCurrency
5. **Audit Trail**: GeneratePlayStreamEvent=true registra em PlayFab

---

## Próximos Passos (Opcional)

- [ ] Adicionar animação ao spinner
- [ ] Implementar cooldown entre compras (ex: 1s)
- [ ] Persistir item no UserData após compra
- [ ] Consumir item via ConsumeItem API se necessário
- [ ] Analytics: logar eventos de compra
- [ ] A/B Testing: Mostrar diferentes preços por jogador

---

## Troubleshooting

| Problema | Solução |
|----------|---------|
| PurchaseProgressManager não visto | Unity precisa recompilar após criar script |
| Modal não aparece | Verificar se PurchaseConfirmationPanel está assignado em StoreUIController |
| CloudScript retorna erro "No function named" | Publicar o script em PlayFab antes de testar |
| Saldo não atualiza | Verificar se OnPurchaseCompletedSecure está sendo chamado |
| Timeout muito frequente | Aumentar purchaseTimeoutSeconds em PurchaseProgressManager |

---

**Versão**: 1.0  
**Data**: Maio 2026  
**Status**: Pronto para teste em dev
