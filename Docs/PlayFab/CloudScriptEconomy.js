handlers.EconomyAddCurrency = function (args, context) {
    var currencyCode = args && args.currencyCode ? args.currencyCode : null;
    var amount = args && args.amount ? parseInt(args.amount, 10) : 0;

    if (!currencyCode) {
        return { success: false, error: "currencyCode is required" };
    }

    if (!amount || amount <= 0) {
        return { success: false, error: "amount must be > 0" };
    }

    var addResult = server.AddUserVirtualCurrency({
        PlayFabId: currentPlayerId,
        VirtualCurrency: currencyCode,
        Amount: amount
    });

    return {
        success: true,
        operation: "add",
        currencyCode: currencyCode,
        balance: addResult.Balance
    };
};

handlers.EconomySubtractCurrency = function (args, context) {
    var currencyCode = args && args.currencyCode ? args.currencyCode : null;
    var amount = args && args.amount ? parseInt(args.amount, 10) : 0;

    if (!currencyCode) {
        return { success: false, error: "currencyCode is required" };
    }

    if (!amount || amount <= 0) {
        return { success: false, error: "amount must be > 0" };
    }

    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var currentBalance = inventory.VirtualCurrency && inventory.VirtualCurrency[currencyCode]
        ? inventory.VirtualCurrency[currencyCode]
        : 0;

    if (currentBalance < amount) {
        return {
            success: false,
            error: "insufficient balance",
            currencyCode: currencyCode,
            balance: currentBalance
        };
    }

    var subtractResult = server.SubtractUserVirtualCurrency({
        PlayFabId: currentPlayerId,
        VirtualCurrency: currencyCode,
        Amount: amount
    });

    return {
        success: true,
        operation: "subtract",
        currencyCode: currencyCode,
        balance: subtractResult.Balance
    };
};

handlers.EconomyGetBalance = function (args, context) {
    var currencyCode = args && args.currencyCode ? args.currencyCode : null;

    if (!currencyCode) {
        return { success: false, error: "currencyCode is required" };
    }

    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var balance = inventory.VirtualCurrency && inventory.VirtualCurrency[currencyCode]
        ? inventory.VirtualCurrency[currencyCode]
        : 0;

    return {
        success: true,
        operation: "getBalance",
        currencyCode: currencyCode,
        balance: balance
    };
};

/**
 * PurchaseItemSecure - Server-authoritative purchase handler
 * 
 * Valida saldo suficiente, subtrai moeda virtual, concede o item e retorna novo saldo.
 * 
 * Args:
 * - itemId (required): ID do item a ser comprado
 * - virtualCurrency (required): Código da moeda (ex: "DA")
 * - price (required): Preço do item
 * - storeId (optional): ID da loja (usado apenas para logging/auditoria)
 * 
 * Returns:
 * - success: true se compra realizada com sucesso
 * - error: mensagem de erro se falhar
 * - itemId: ID do item comprado
 * - currencyCode: Moeda utilizada
 * - priceDeducted: Preço debitado
 * - newBalance: Novo saldo de moeda após compra
 * - operation: "purchase"
 */
handlers.PurchaseItemSecure = function (args, context) {
    var itemId = args && args.itemId ? String(args.itemId) : null;
    var virtualCurrency = args && args.virtualCurrency ? String(args.virtualCurrency) : null;
    var price = args && args.price ? parseInt(args.price, 10) : 0;
    var storeId = args && args.storeId ? String(args.storeId) : "store_default";

    // Validações de entrada
    if (!itemId) {
        return { success: false, error: "itemId is required" };
    }

    if (!virtualCurrency) {
        return { success: false, error: "virtualCurrency is required" };
    }

    if (!price || price <= 0) {
        return { success: false, error: "price must be > 0" };
    }

    // Obter saldo atual e inventario para validar compra unica de decks/skins
    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var currentBalance = inventory.VirtualCurrency && inventory.VirtualCurrency[virtualCurrency]
        ? inventory.VirtualCurrency[virtualCurrency]
        : 0;

    var isUniqueItem = itemId.toLowerCase().indexOf("deck") === 0
        || itemId.toLowerCase().indexOf("skin") === 0;

    if (isUniqueItem && inventory.Inventory) {
        for (var ownedIndex = 0; ownedIndex < inventory.Inventory.length; ownedIndex++) {
            var ownedItem = inventory.Inventory[ownedIndex];
            if (ownedItem && ownedItem.ItemId && ownedItem.ItemId.toLowerCase() === itemId.toLowerCase()) {
                return {
                    success: false,
                    error: "already_owned",
                    itemId: itemId,
                    currencyCode: virtualCurrency,
                    currentBalance: currentBalance
                };
            }
        }
    }

    // Validar saldo suficiente
    if (currentBalance < price) {
        return {
            success: false,
            error: "insufficient_balance",
            itemId: itemId,
            currencyCode: virtualCurrency,
            price: price,
            currentBalance: currentBalance
        };
    }

    // Validar se item existe no catálogo ou store
    var catalogItems = server.GetCatalogItems({ CatalogVersion: "mainCatalog" });
    var itemFound = false;

    if (catalogItems && catalogItems.Catalog) {
        for (var i = 0; i < catalogItems.Catalog.length; i++) {
            if (catalogItems.Catalog[i].ItemId === itemId) {
                itemFound = true;
                break;
            }
        }
    }

    if (!itemFound) {
        return {
            success: false,
            error: "item_not_found",
            itemId: itemId
        };
    }

    // Subtrair moeda virtual
    var subtractResult = server.SubtractUserVirtualCurrency({
        PlayFabId: currentPlayerId,
        VirtualCurrency: virtualCurrency,
        Amount: price
    });

    var newBalance = subtractResult.Balance !== undefined ? subtractResult.Balance : (currentBalance - price);
    var grantResult = null;

    try {
        grantResult = server.GrantItemsToUser({
            PlayFabId: currentPlayerId,
            CatalogVersion: "mainCatalog",
            ItemIds: [itemId],
            Annotation: "PurchaseItemSecure:" + storeId
        });
    } catch (grantError) {
        var refundResult = server.AddUserVirtualCurrency({
            PlayFabId: currentPlayerId,
            VirtualCurrency: virtualCurrency,
            Amount: price
        });

        return {
            success: false,
            error: "grant_failed_refunded",
            itemId: itemId,
            currencyCode: virtualCurrency,
            refundedAmount: price,
            newBalance: refundResult.Balance !== undefined ? refundResult.Balance : currentBalance,
            details: grantError && grantError.message ? grantError.message : String(grantError)
        };
    }

    var grantedItemInstanceIds = [];
    if (grantResult && grantResult.ItemGrantResults) {
        for (var grantIndex = 0; grantIndex < grantResult.ItemGrantResults.length; grantIndex++) {
            var granted = grantResult.ItemGrantResults[grantIndex];
            if (granted && granted.ItemInstanceId) {
                grantedItemInstanceIds.push(granted.ItemInstanceId);
            }
        }
    }

    return {
        success: true,
        operation: "purchase",
        itemId: itemId,
        currencyCode: virtualCurrency,
        priceDeducted: price,
        newBalance: newBalance,
        grantedItemInstanceIds: grantedItemInstanceIds,
        storeId: storeId
    };
};
