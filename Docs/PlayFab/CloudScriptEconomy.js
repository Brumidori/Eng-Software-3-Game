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
