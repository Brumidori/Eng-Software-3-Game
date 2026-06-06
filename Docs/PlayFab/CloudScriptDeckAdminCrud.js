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

/*
 * CloudScript - Deck Admin CRUD (TitleData)
 *
 * Handlers:
 * - ValidatePlayerRole
 * - GrantStarterDecks
 * - DeckAdminListCatalog
 * - DeckAdminGetDeck
 * - DeckAdminCreateDeck
 * - DeckAdminUpdateDeck
 * - DeckAdminDeleteDeck
 * - DeckAdminValidateDeckPayload
 *
 * Data model in TitleData:
 * - deck_index: { versao: number, categorias: [{ nome: string, key: string }] }
 * - cartas_<tema>: {
 *     deck_id: string,
 *     theme: string,
 *     questions: [
 *       {
 *         id: string,
 *         text: string,
 *         options: [{ text: string, is_correct: boolean }],
 *         time_limit: number
 *       }
 *     ]
 *   }
 */

var DECK_INDEX_KEY = "deck_index";
var ROLE_KEY = "role";
var ADMIN_ROLE = "admin";
var DEFAULT_CATALOG_VERSION = "mainCatalog";
var DEFAULT_STARTER_SKIN_ID = "skinDefault";

handlers.ValidatePlayerRole = function (args, context) {
    var roleResult = getCurrentPlayerRole();
    if (!roleResult.success) {
        return roleResult;
    }

    return {
        success: true,
        role: roleResult.role
    };
};

handlers.GrantStarterDecks = function (args, context) {
    return grantStarterDecksCore(args, context);
};

function grantStarterDecksCore(args, context) {
    try {
        var catalogVersion = args && args.catalogVersion ? String(args.catalogVersion) : DEFAULT_CATALOG_VERSION;

        var catalogResult = server.GetCatalogItems({ CatalogVersion: catalogVersion });
        var eligibleItemIds = findStarterDeckItemIds(catalogResult);
        addUniqueItemId(eligibleItemIds, DEFAULT_STARTER_SKIN_ID);

        if (eligibleItemIds.length === 0) {
            return fail("no starter decks configured for catalog version: " + catalogVersion);
        }

        var inventoryResult = server.GetUserInventory({ PlayFabId: currentPlayerId });
        var ownedItemIds = buildOwnedItemIdMap(inventoryResult);
        var missingItemIds = [];

        for (var i = 0; i < eligibleItemIds.length; i++) {
            var itemId = eligibleItemIds[i];
            if (!ownedItemIds[itemId]) {
                missingItemIds.push(itemId);
            }
        }

        if (missingItemIds.length === 0) {
            return {
                success: true,
                alreadyGranted: true,
                catalogVersion: catalogVersion,
                grantedItemIds: eligibleItemIds,
                operation: "grantStarterDecks"
            };
        }

        server.GrantItemsToUser({
            PlayFabId: currentPlayerId,
            CatalogVersion: catalogVersion,
            ItemIds: missingItemIds,
            Annotation: "Starter deck grant on registration"
        });

        return {
            success: true,
            alreadyGranted: false,
            catalogVersion: catalogVersion,
            grantedItemIds: missingItemIds,
            eligibleItemIds: eligibleItemIds,
            operation: "grantStarterDecks"
        };
    } catch (error) {
        return fail("unexpected error while granting starter decks", error);
    }
}

handlers.DeckAdminListCatalog = function(args) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;

    return {
        success: true,
        deckIndex: indexResult.deckIndex
    };
};


handlers.DeckAdminToggleDeck = function(args, context) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var key = args && args.key ? String(args.key) : "";
    if (!key) return fail("key is required");

    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;

    var deckIndex = indexResult.deckIndex;
    var category = findCategoryByKey(deckIndex, key);
    if (!category) return fail("key not found in deck_index: " + key);

    category.ativo = category.ativo === false ? true : false;

    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) return saveResult;

    return {
        success: true,
        operation: "toggle",
        key: key,
        ativo: category.ativo,
        versao: deckIndex.versao
    };
};



handlers.DeckAdminGetDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) {
        return guard;
    }

    var key = args && args.key ? String(args.key) : "";
    if (!key) {
        return fail("key is required");
    }

    var indexResult = loadDeckIndex();
    if (!indexResult.success) {
        return indexResult;
    }

    var category = findCategoryByKey(indexResult.deckIndex, key);
    if (!category) {
        return fail("deck key not found in deck_index");
    }

    var titleData = server.GetTitleData({ Keys: [key] });
    var rawDeck = titleData && titleData.Data ? titleData.Data[key] : null;

    if (!rawDeck) {
        return fail("deck not found in TitleData for key: " + key);
    }

    var parsed = safeJsonParse(rawDeck);
    if (!parsed.success) {
        return fail("invalid deck JSON in TitleData: " + key, parsed.error);
    }

    var validation = validateDeckPayload(parsed.value, category.nome, key);
    if (!validation.success) {
        return fail("deck payload is invalid", validation.errors);
    }

    return {
        success: true,
        operation: "get",
        key: key,
        nome: category.nome,
        deck: parsed.value,
        deckJson: JSON.stringify(parsed.value),
        validationWarnings: validation.warnings
    };
};

handlers.DeckAdminCreateDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) {
        return guard;
    }

    var nome = args && args.nome ? String(args.nome) : "";
    var key = args && args.key ? String(args.key) : "";
    var deckPayload = args ? args.deck : null;

    if (!nome) {
        return fail("nome is required");
    }

    if (!key) {
        return fail("key is required");
    }

    var keyValidation = validateDeckKey(key);
    if (!keyValidation.success) {
        return keyValidation;
    }

    var validation = validateDeckPayload(deckPayload, nome, key);
    if (!validation.success) {
        return fail("deck validation failed", validation.errors);
    }

    var indexResult = loadDeckIndex();
    if (!indexResult.success) {
        return indexResult;
    }

    var deckIndex = indexResult.deckIndex;

    if (findCategoryByKey(deckIndex, key)) {
        return fail("key already exists in deck_index");
    }

    if (findCategoryByName(deckIndex, nome)) {
        return fail("nome already exists in deck_index");
    }

    var normalizedDeck = normalizeDeckPayload(deckPayload, nome);

    server.SetTitleData({
        Key: key,
        Value: JSON.stringify(normalizedDeck)
    });

    deckIndex.categorias.push({ nome: nome, key: key });
    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) {
        return saveResult;
    }

    return {
        success: true,
        operation: "create",
        key: key,
        nome: nome,
        versao: deckIndex.versao,
        warnings: validation.warnings
    };
};

handlers.DeckAdminUpdateDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) {
        return guard;
    }

    var key = args && args.key ? String(args.key) : "";
    var novoNome = args && args.nome ? String(args.nome) : null;
    var deckPayload = args && args.deck ? args.deck : null;

    if (!key) {
        return fail("key is required");
    }

    var indexResult = loadDeckIndex();
    if (!indexResult.success) {
        return indexResult;
    }

    var deckIndex = indexResult.deckIndex;
    var category = findCategoryByKey(deckIndex, key);

    if (!category) {
        return fail("key not found in deck_index");
    }

    if (!novoNome && !deckPayload) {
        return fail("nothing to update: send nome and/or deck");
    }

    if (novoNome) {
        var duplicateByName = findCategoryByName(deckIndex, novoNome);
        if (duplicateByName && duplicateByName.key !== key) {
            return fail("nome already exists in another category");
        }
    }

    var targetName = novoNome || category.nome;

    var validation = null;
    if (deckPayload) {
        validation = validateDeckPayload(deckPayload, targetName, key);
        if (!validation.success) {
            return fail("deck validation failed", validation.errors);
        }

        var normalizedDeck = normalizeDeckPayload(deckPayload, targetName);
        server.SetTitleData({
            Key: key,
            Value: JSON.stringify(normalizedDeck)
        });
    }

    if (novoNome) {
        category.nome = novoNome;
    }

    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) {
        return saveResult;
    }

    return {
        success: true,
        operation: "update",
        key: key,
        nome: category.nome,
        versao: deckIndex.versao,
        warnings: validation ? validation.warnings : []
    };
};

handlers.DeckAdminDeleteDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) {
        return guard;
    }

    var key = args && args.key ? String(args.key) : "";
    var clearDeckContent = !args || args.clearDeckContent !== false;

    if (!key) {
        return fail("key is required");
    }

    var indexResult = loadDeckIndex();
    if (!indexResult.success) {
        return indexResult;
    }

    var deckIndex = indexResult.deckIndex;
    var category = findCategoryByKey(deckIndex, key);

    if (!category) {
        return fail("key not found in deck_index");
    }

    var kept = [];
    for (var i = 0; i < deckIndex.categorias.length; i++) {
        if (deckIndex.categorias[i].key !== key) {
            kept.push(deckIndex.categorias[i]);
        }
    }

    deckIndex.categorias = kept;
    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) {
        return saveResult;
    }

    var keyDeleteResult = {
        success: true,
        keyDeleted: false,
        mode: "skipped"
    };

    if (clearDeckContent) {
        keyDeleteResult = deleteDeckTitleDataKey(key);
        if (!keyDeleteResult.success) {
            return fail("deck removed from deck_index but failed to delete TitleData key", keyDeleteResult.error);
        }
    }

    return {
        success: true,
        operation: "delete",
        key: key,
        removedCategoryName: category.nome,
        versao: deckIndex.versao,
        contentCleared: clearDeckContent,
        keyDeleted: keyDeleteResult.keyDeleted,
        keyDeleteMode: keyDeleteResult.mode
    };
};

handlers.DeckAdminValidateDeckPayload = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) {
        return guard;
    }

    var nome = args && args.nome ? String(args.nome) : "";
    var key = args && args.key ? String(args.key) : "";
    var deckPayload = args ? args.deck : null;

    var result = validateDeckPayload(deckPayload, nome, key);
    return {
        success: result.success,
        errors: result.errors,
        warnings: result.warnings
    };
};

function requireAdmin() {
    var roleResult = getCurrentPlayerRole();
    if (!roleResult.success) {
        return roleResult;
    }

    if (String(roleResult.role).toLowerCase() !== ADMIN_ROLE) {
        return fail("forbidden: admin role required");
    }

    return { success: true };
}

function getCurrentPlayerRole() {
    try {
        var userInternal = server.GetUserInternalData({
            PlayFabId: currentPlayerId,
            Keys: [ROLE_KEY]
        });

        if (!userInternal || !userInternal.Data || !userInternal.Data[ROLE_KEY] || !userInternal.Data[ROLE_KEY].Value) {
            return fail("role not found for current user");
        }

        return {
            success: true,
            role: String(userInternal.Data[ROLE_KEY].Value)
        };
    } catch (error) {
        return fail("failed to get user role", error);
    }
}

function loadDeckIndex() {
    var titleData = server.GetTitleData({ Keys: [DECK_INDEX_KEY] });
    var raw = titleData && titleData.Data ? titleData.Data[DECK_INDEX_KEY] : null;

    if (!raw) {
        return fail("deck_index not found in TitleData");
    }

    var parsed = safeJsonParse(raw);
    if (!parsed.success) {
        return fail("invalid JSON in deck_index", parsed.error);
    }

    var deckIndex = parsed.value;
    if (!deckIndex || typeof deckIndex !== "object") {
        return fail("deck_index payload must be an object");
    }

    if (!Array.isArray(deckIndex.categorias)) {
        return fail("deck_index.categorias must be an array");
    }

    if (typeof deckIndex.versao !== "number") {
        deckIndex.versao = 1;
    }

    return {
        success: true,
        deckIndex: deckIndex
    };
}

function findStarterDeckItemIds(catalogResult) {
    var eligible = [];
    var seen = {};

    if (!catalogResult || !Array.isArray(catalogResult.Catalog)) {
        return eligible;
    }

    for (var i = 0; i < catalogResult.Catalog.length; i++) {
        var item = catalogResult.Catalog[i];
        if (!item || !isStarterDeckCatalogItem(item)) {
            continue;
        }

        var itemId = String(item.ItemId || "").trim();
        if (!itemId || seen[itemId]) {
            continue;
        }

        seen[itemId] = true;
        eligible.push(itemId);
    }

    return eligible;
}

function addUniqueItemId(itemIds, itemId) {
    if (!Array.isArray(itemIds) || !isNonEmptyString(itemId)) {
        return;
    }

    var normalized = String(itemId).toLowerCase();
    for (var i = 0; i < itemIds.length; i++) {
        if (String(itemIds[i]).toLowerCase() === normalized) {
            return;
        }
    }

    itemIds.push(itemId);
}

function isStarterDeckCatalogItem(item) {
    if (!item || !isNonEmptyString(item.ItemId)) {
        return false;
    }

    var itemId = String(item.ItemId).toLowerCase();
    if (itemId.indexOf("deck") !== 0) {
        return false;
    }

    if (!isNonEmptyString(item.CustomData)) {
        return false;
    }

    var parsed = safeJsonParse(item.CustomData);
    if (!parsed.success || !parsed.value || typeof parsed.value !== "object") {
        return false;
    }

    return isTruthyStarterFlag(parsed.value.is_starter);
}

function isTruthyStarterFlag(value) {
    return value === true || String(value).toLowerCase() === "true";
}

function buildOwnedItemIdMap(inventoryResult) {
    var owned = {};

    if (!inventoryResult || !Array.isArray(inventoryResult.Inventory)) {
        return owned;
    }

    for (var i = 0; i < inventoryResult.Inventory.length; i++) {
        var item = inventoryResult.Inventory[i];
        if (item && isNonEmptyString(item.ItemId)) {
            owned[String(item.ItemId)] = true;
        }
    }

    return owned;
}

function saveDeckIndex(deckIndex) {
    if (!deckIndex || typeof deckIndex !== "object") {
        return fail("deckIndex is invalid");
    }

    if (!Array.isArray(deckIndex.categorias)) {
        return fail("deckIndex.categorias must be array");
    }

    server.SetTitleData({
        Key: DECK_INDEX_KEY,
        Value: JSON.stringify(deckIndex)
    });

    return { success: true };
}

function deleteDeckTitleDataKey(key) {
    if (!isNonEmptyString(key)) {
        return fail("key is required for TitleData deletion");
    }

    try {
        if (server && typeof server.DeleteTitleData === "function") {
            server.DeleteTitleData({ Key: key });
            return {
                success: true,
                keyDeleted: true,
                mode: "DeleteTitleData"
            };
        }

        // Compatibility fallback for environments where DeleteTitleData is unavailable.
        server.SetTitleData({
            Key: key,
            Value: null
        });

        var check = server.GetTitleData({ Keys: [key] });
        var hasDataMap = check && check.Data && typeof check.Data === "object";
        var hasKey = hasDataMap && Object.prototype.hasOwnProperty.call(check.Data, key);
        var keyValue = hasKey ? check.Data[key] : null;
        var consideredDeleted = !hasKey || keyValue === null || keyValue === "";

        if (!consideredDeleted) {
            return fail("DeleteTitleData unavailable and fallback did not remove key");
        }

        return {
            success: true,
            keyDeleted: true,
            mode: "SetTitleData(null)"
        };
    } catch (error) {
        return fail("failed deleting deck TitleData key", error);
    }
}

function validateDeckKey(key) {
    if (!key) {
        return fail("key is required");
    }

    return { success: true };
}

function validateDeckPayload(payload, categoriaNome, categoriaKey) {
    var errors = [];
    var warnings = [];

    if (!payload || typeof payload !== "object") {
        errors.push("deck payload must be an object");
        return resultValidation(errors, warnings);
    }

    if (!isNonEmptyString(payload.deck_id)) {
        errors.push("deck_id is required");
    }

    if (!isNonEmptyString(payload.theme)) {
        errors.push("theme is required");
    }

    if (!Array.isArray(payload.questions)) {
        errors.push("questions must be an array");
        return resultValidation(errors, warnings);
    }

    if (payload.questions.length === 0) {
        errors.push("questions must have at least one entry");
        return resultValidation(errors, warnings);
    }

    var ids = {};

    for (var i = 0; i < payload.questions.length; i++) {
        var question = payload.questions[i];
        var label = "questions[" + i + "]";

        if (!question || typeof question !== "object") {
            errors.push(label + " must be an object");
            continue;
        }

        if (!isNonEmptyString(question.id)) {
            errors.push(label + ".id is required");
        } else if (ids[question.id]) {
            errors.push(label + ".id is duplicated: " + question.id);
        } else {
            ids[question.id] = true;
        }

        if (!isNonEmptyString(question.text)) {
            errors.push(label + ".text is required");
        }

        if (!Array.isArray(question.options)) {
            errors.push(label + ".options must be an array");
        } else {
            if (question.options.length < 4) {
                errors.push(label + ".options must have at least 4 entries");
            }

            var correctCount = 0;
            for (var j = 0; j < question.options.length; j++) {
                var option = question.options[j];
                var optionLabel = label + ".options[" + j + "]";

                if (!option || typeof option !== "object") {
                    errors.push(optionLabel + " must be an object");
                    continue;
                }

                if (!isNonEmptyString(option.text)) {
                    errors.push(optionLabel + ".text must be a non-empty string");
                }

                if (typeof option.is_correct !== "boolean") {
                    errors.push(optionLabel + ".is_correct must be boolean");
                } else if (option.is_correct) {
                    correctCount++;
                }
            }

            if (correctCount !== 1) {
                errors.push(label + " must have exactly one option with is_correct=true");
            }
        }

        if (typeof question.time_limit !== "number" || Math.floor(question.time_limit) !== question.time_limit || question.time_limit < 1) {
            errors.push(label + ".time_limit must be an integer >= 1");
        }
    }

    if (isNonEmptyString(payload.theme) && isNonEmptyString(categoriaNome) && payload.theme !== categoriaNome) {
        warnings.push("theme differs from category name: " + payload.theme);
    }

    if (isNonEmptyString(categoriaKey)) {
        var keyResult = validateDeckKey(categoriaKey);
        if (!keyResult.success) {
            errors.push(keyResult.error);
        }
    }

    return resultValidation(errors, warnings);
}

function normalizeDeckPayload(payload, categoriaNome) {
    var out = {
        deck_id: String(payload.deck_id).trim(),
        theme: isNonEmptyString(payload.theme) ? String(payload.theme).trim() : String(categoriaNome || "").trim(),
        questions: []
    };

    for (var i = 0; i < payload.questions.length; i++) {
        var question = payload.questions[i];
        var normalizedOptions = [];

        for (var j = 0; j < question.options.length; j++) {
            normalizedOptions.push({
                text: String(question.options[j].text).trim(),
                is_correct: question.options[j].is_correct === true
            });
        }

        out.questions.push({
            id: String(question.id).trim(),
            text: String(question.text).trim(),
            options: normalizedOptions,
            time_limit: parseInt(question.time_limit, 10)
        });
    }

    return out;
}

function findCategoryByKey(deckIndex, key) {
    if (!deckIndex || !Array.isArray(deckIndex.categorias)) {
        return null;
    }

    for (var i = 0; i < deckIndex.categorias.length; i++) {
        if (deckIndex.categorias[i] && deckIndex.categorias[i].key === key) {
            return deckIndex.categorias[i];
        }
    }

    return null;
}

function findCategoryByName(deckIndex, nome) {
    if (!deckIndex || !Array.isArray(deckIndex.categorias)) {
        return null;
    }

    for (var i = 0; i < deckIndex.categorias.length; i++) {
        if (deckIndex.categorias[i] && deckIndex.categorias[i].nome === nome) {
            return deckIndex.categorias[i];
        }
    }

    return null;
}

function safeJsonParse(raw) {
    try {
        return { success: true, value: JSON.parse(raw) };
    } catch (error) {
        return fail("json parse error", error);
    }
}

function isNonEmptyString(value) {
    return typeof value === "string" && value.trim().length > 0;
}

function toPositiveInt(value, fallback) {
    if (typeof value === "number" && value >= 1 && Math.floor(value) === value) {
        return value;
    }
    return fallback;
}

function resultValidation(errors, warnings) {
    return {
        success: errors.length === 0,
        errors: errors,
        warnings: warnings
    };
}

function fail(message, details) {
    var result = {
        success: false,
        error: message
    };

    if (typeof details !== "undefined" && details !== null) {
        result.details = details;
    }

    return result;
}

var MATCH_CONFIG = {
    InitialHP:              100,
    MaxRounds:              20,
    ThemePhaseDurationMs:   4000,
    QuestionPhaseDurationMs: 20000,
    SpeedBonusThresholdMs:  200,
    AfkRoundLimit:          3,
    ReconnectWindowMs:      30000
};

var DAMAGE_CONFIG = {
    BaseDamage:         5,
    SpeedBonus:         2,
    SelfDamageNoAnswer: 3,
    BetBonus:           5,
    StealAmount:        5
};

function getStreakBonus(streak) {
    if (streak <= 1) return 0;
    if (streak === 2) return 1;
    if (streak === 3) return 3;
    return 5;
}

// ============================================================
// STORAGE — Title Internal Data
// Cada partida salva com chave "match_{matchId}"
// ============================================================

function saveMatchState(state) {
    server.SetTitleInternalData({
        Key:   "match_" + state.MatchId,
        Value: JSON.stringify(state)
    });
}

function loadMatchState(matchId) {
    var result = server.GetTitleInternalData({
        Keys: ["match_" + matchId]
    });
    var json = result.Data["match_" + matchId];
    return json ? JSON.parse(json) : null;
}

function deleteMatchState(matchId) {
    server.SetTitleInternalData({ Key: "match_" + matchId, Value: null });
    removeFromActiveIndex(matchId);
}

// Índice de partidas ativas (usado pelo watchdog de AFK)
function loadActiveIndex() {
    var result = server.GetTitleInternalData({ Keys: ["active_matches_index"] });
    var json   = result.Data["active_matches_index"];
    return json ? JSON.parse(json) : [];
}

function saveActiveIndex(ids) {
    server.SetTitleInternalData({
        Key:   "active_matches_index",
        Value: JSON.stringify(ids)
    });
}

function addToActiveIndex(matchId) {
    var ids = loadActiveIndex();
    if (ids.indexOf(matchId) === -1) { ids.push(matchId); saveActiveIndex(ids); }
}

function removeFromActiveIndex(matchId) {
    var ids = loadActiveIndex();
    var idx = ids.indexOf(matchId);
    if (idx !== -1) { ids.splice(idx, 1); saveActiveIndex(ids); }
}

// ============================================================
// HANDLER: CreateMatch
// Chamado pelo cliente após matchmaking encontrar partida.
// Idempotente — retorna estado existente se já criado.
// ============================================================

handlers.CreateMatch = function(args) {
    var matchId   = args.matchId;
    var player1Id = args.player1Id;
    var player2Id = args.player2Id;

    var existing = loadMatchState(matchId);
    if (existing && existing.IsActive)
        return { success: true, matchId: matchId, networkDescriptor: existing.PartyNetworkDescriptor };

    var p1PowerUp    = getEquippedPowerUp(player1Id);
    var p2PowerUp    = getEquippedPowerUp(player2Id);
    var questionPool = buildQuestionPool(player1Id, player2Id);

    var state = {
        MatchId:    matchId,
        Player1Id:  player1Id,
        Player2Id:  player2Id,
        Player1State: makePlayerState(player1Id, p1PowerUp),
        Player2State: makePlayerState(player2Id, p2PowerUp),
        CurrentRound:          0,
        Phase:                 "Initializing",
        PhaseStartTimestampMs: 0,
        QuestionPool:          questionPool,
        CurrentRoundState:     null,
        IsActive:              true,
        WinnerId:              null,
        EndReason:             null,
        MatchStartTimestampMs: Date.now(),
        PartyNetworkDescriptor: "brainduel_" + matchId,
        LastProcessedRound:    0
    };

    saveMatchState(state);
    addToActiveIndex(matchId);

    return { success: true, matchId: matchId, networkDescriptor: state.PartyNetworkDescriptor };
};

// ============================================================
// HANDLER: StartNextRound
// Prepara rodada e retorna dados do tema ao cliente,
// que faz o broadcast via Party.
// ============================================================

handlers.StartNextRound = function(args) {
    var matchId     = args.matchId;
    var roundNumber = args.roundNumber;

    var state = loadMatchState(matchId);
    if (!state || !state.IsActive)     return { error: "Match inativo" };
    if (state.CurrentRound >= roundNumber) return { status: "already_started" };

    if (roundNumber > MATCH_CONFIG.MaxRounds) {
        var winner = determineWinnerByHP(state);
        return finalizeMatch(state, winner, "RoundsOver");
    }

    var question = getQuestionForRound(state, roundNumber);
    if (!question) return { error: "Pergunta nao encontrada para rodada " + roundNumber };

    state.CurrentRound          = roundNumber;
    state.Phase                 = "ThemeAndPowerUp";
    state.PhaseStartTimestampMs = Date.now();
    state.CurrentRoundState     = {
        RoundNumber:     roundNumber,
        QuestionId:      question.QuestionId,
        ThemeId:         question.ThemeId,
        ThemeName:       question.ThemeName,
        CorrectAnswerId: question.CorrectOptionId,
        Player1Action:   makeAction(state.Player1Id),
        Player2Action:   makeAction(state.Player2Id),
        IsProcessed:     false,
        Player1Result:   null,
        Player2Result:   null
    };

    saveMatchState(state);

    return {
        status:            "ok",
        roundNumber:       roundNumber,
        themeId:           question.ThemeId,
        themeName:         question.ThemeName,
        serverTimestampMs: state.PhaseStartTimestampMs,
        themeDurationMs:   MATCH_CONFIG.ThemePhaseDurationMs,
        questionDurationMs: MATCH_CONFIG.QuestionPhaseDurationMs
    };
};

// ============================================================
// HANDLER: StartQuestion
// Transita para fase de pergunta e retorna dados da questão.
// ============================================================

handlers.StartQuestion = function(args) {
    var state = loadMatchState(args.matchId);
    if (!state || state.CurrentRound !== args.roundNumber) return { status: "ignored" };
    if (state.Phase !== "ThemeAndPowerUp") return { status: "wrong_phase" };

    state.Phase                 = "Question";
    state.PhaseStartTimestampMs = Date.now();
    saveMatchState(state);

    var question = loadQuestion(state.CurrentRoundState.QuestionId);
    return {
        status:           "ok",
        questionId:       question.QuestionId,
        questionText:     question.Text,
        answers:          question.Options,
        serverTimestampMs: state.PhaseStartTimestampMs,
        durationMs:       MATCH_CONFIG.QuestionPhaseDurationMs
    };
};

// ============================================================
// HANDLER: SubmitAnswer
// Registra resposta. Se ambos responderam, processa a rodada.
// ============================================================

handlers.SubmitAnswer = function(args) {
    var playerId = currentPlayerId;
    var state    = loadMatchState(args.matchId);

    if (!state || !state.IsActive)              return { error: "Match inativo" };
    if (state.CurrentRound !== args.roundNumber) return { status: "wrong_round" };
    if (state.Phase !== "Question")              return { status: "wrong_phase" };

    var action = getPlayerAction(state, playerId);
    if (action.HasAnswered) return { status: "already_answered" };

    action.AnswerId          = args.answerId;
    action.AnswerTimestampMs = args.clientTimestampMs || Date.now();
    action.HasAnswered       = true;

    saveMatchState(state);

    var roundResult = null;
    if (bothPlayersAnswered(state))
        roundResult = processRoundInternal(state);

    return { status: "ok", roundResult: roundResult };
};

// ============================================================
// HANDLER: ActivatePowerUp
// Só aceito durante ThemeAndPowerUp (janela de 4s).
// ============================================================

handlers.ActivatePowerUp = function(args) {
    var playerId = currentPlayerId;
    var state    = loadMatchState(args.matchId);

    if (!state || !state.IsActive)              return { error: "Match inativo" };
    if (state.CurrentRound !== args.roundNumber) return { status: "wrong_round" };
    if (state.Phase !== "ThemeAndPowerUp")       return { error: "Fora da janela de power-up" };

    var ps     = getPlayerState(state, playerId);
    var action = getPlayerAction(state, playerId);

    if (ps.HasUsedPowerUp)              return { error: "Power-up ja usado nesta partida" };
    if (ps.EquippedPowerUp !== args.powerUp) return { error: "Power-up nao equipado" };

    action.ActivatedPowerUp = args.powerUp;
    saveMatchState(state);

    return { status: "ok" };
};

// ============================================================
// HANDLER: ProcessRound
// Chamado por ambos os clientes quando o timer de 20s acaba.
// Idempotente: segunda chamada retorna resultado cacheado.
// ============================================================

handlers.ProcessRound = function(args) {
    var state = loadMatchState(args.matchId);
    if (!state || !state.IsActive)              return { error: "Match inativo" };
    if (state.CurrentRound !== args.roundNumber) return { status: "wrong_round" };

    if (state.CurrentRoundState.IsProcessed)
        return buildRoundResponse(state, true);

    return processRoundInternal(state);
};

// ============================================================
// HANDLER: RejoinMatch
// Reconecta jogador e retorna estado completo da partida.
// ============================================================

handlers.RejoinMatch = function(args) {
    var playerId = currentPlayerId;
    var state    = loadMatchState(args.matchId);
    if (!state || !state.IsActive) return { error: "Match nao encontrado" };

    var ps = getPlayerState(state, playerId);
    if (ps) { ps.IsConnected = true; ps.ConsecutiveMissedRounds = 0; }

    saveMatchState(state);

    return {
        status:           "ok",
        fullState:        state,
        currentQuestion:  state.Phase === "Question" ? loadQuestion(state.CurrentRoundState.QuestionId) : null,
        serverTimestampMs: Date.now()
    };
};

// ============================================================
// HANDLER: FinalizeMatch
// Chamado pelo cliente após MatchEnd para registrar resultado.
// ============================================================

handlers.FinalizeMatch = function(args) {
    var state = loadMatchState(args.matchId);
    if (!state) return { error: "Match nao encontrado" };
    finalizeMatch(state, args.winnerId, "HPDepleted");
    return { status: "ok" };
};

// ============================================================
// LÓGICA INTERNA DE RODADA (server-authoritative)
// ============================================================

function processRoundInternal(state) {
    var round   = state.CurrentRoundState;
    var p1Act   = round.Player1Action;
    var p2Act   = round.Player2Action;
    var p1State = state.Player1State;
    var p2State = state.Player2State;

    var p1Result = computePlayerResult(p1Act, p1State, p2Act, p2State, round.CorrectAnswerId);
    var p2Result = computePlayerResult(p2Act, p2State, p1Act, p1State, round.CorrectAnswerId);

    var p1HPBefore = p1State.HP;
    var p2HPBefore = p2State.HP;

    // Aplica dano base
    if (!p2Result.WasShielded) p2State.HP -= p1Result.DamageDealt;
    if (!p1Result.WasShielded) p1State.HP -= p2Result.DamageDealt;

    // Steal (independente do shield — roubo direto)
    if (p1Act.ActivatedPowerUp === "Steal") { p1State.HP += DAMAGE_CONFIG.StealAmount; p2State.HP -= DAMAGE_CONFIG.StealAmount; }
    if (p2Act.ActivatedPowerUp === "Steal") { p2State.HP += DAMAGE_CONFIG.StealAmount; p1State.HP -= DAMAGE_CONFIG.StealAmount; }

    // Self-damage por não responder
    if (!p1Act.HasAnswered) p1State.HP -= DAMAGE_CONFIG.SelfDamageNoAnswer;
    if (!p2Act.HasAnswered) p2State.HP -= DAMAGE_CONFIG.SelfDamageNoAnswer;

    p1State.HP = Math.max(0, p1State.HP);
    p2State.HP = Math.max(0, p2State.HP);

    p1Result.HPBefore = p1HPBefore; p1Result.HPAfter = p1State.HP;
    p2Result.HPBefore = p2HPBefore; p2Result.HPAfter = p2State.HP;

    // Streaks
    p1State.Streak = p1Result.Result === "Correct" ? p1State.Streak + 1 : 0;
    p2State.Streak = p2Result.Result === "Correct" ? p2State.Streak + 1 : 0;
    p1Result.StreakAfter = p1State.Streak;
    p2Result.StreakAfter = p2State.Streak;

    // AFK tracking
    if (!p1Act.HasAnswered) p1State.ConsecutiveMissedRounds++;
    else p1State.ConsecutiveMissedRounds = 0;
    if (!p2Act.HasAnswered) p2State.ConsecutiveMissedRounds++;
    else p2State.ConsecutiveMissedRounds = 0;

    // Atualiza cargas de shield
    updatePowerUpCharges(p1State, p1Act);
    updatePowerUpCharges(p2State, p2Act);

    round.Player1Result = p1Result;
    round.Player2Result = p2Result;
    round.IsProcessed   = true;
    state.LastProcessedRound = round.RoundNumber;

    // Verifica fim de partida
    var afkP1     = p1State.ConsecutiveMissedRounds >= MATCH_CONFIG.AfkRoundLimit;
    var afkP2     = p2State.ConsecutiveMissedRounds >= MATCH_CONFIG.AfkRoundLimit;
    var hpOver    = p1State.HP <= 0 || p2State.HP <= 0;
    var roundsMax = round.RoundNumber >= MATCH_CONFIG.MaxRounds;
    var matchOver = hpOver || roundsMax || afkP1 || afkP2;

    var winnerId  = null;
    var endReason = "HPDepleted";

    if (matchOver) {
        if      (afkP1) { winnerId = state.Player2Id; endReason = "Abandonment"; }
        else if (afkP2) { winnerId = state.Player1Id; endReason = "Abandonment"; }
        else if (roundsMax) { winnerId = determineWinnerByHP(state); endReason = "RoundsOver"; }
        else    { winnerId = determineWinnerByHP(state); }

        state.IsActive  = false;
        state.WinnerId  = winnerId;
        state.EndReason = endReason;
        state.Phase     = "Reveal";
    }

    saveMatchState(state);
    if (matchOver) { removeFromActiveIndex(state.MatchId); updatePlayerStats(state, winnerId); }

    return buildRoundResponse(state, false);
}

function computePlayerResult(attackerAct, attackerState, defenderAct, defenderState, correctId) {
    var result = {
        PlayerId:    attackerAct.PlayerId,
        AnsweredId:  attackerAct.AnswerId,
        WasShielded: false,
        DamageDealt: 0,
        Breakdown: { BaseDamage: 0, SpeedBonus: 0, StreakBonus: 0, PowerUpBonus: 0, StolenHP: 0, SelfDamage: 0 }
    };

    if (!attackerAct.HasAnswered) {
        result.Result = "NotAnswered";
        return result; // self-damage aplicado em processRoundInternal
    }

    var correct  = (attackerAct.AnswerId === correctId);
    result.Result = correct ? "Correct" : "Incorrect";

    if (!correct) return result;

    var streak = attackerState.Streak + 1;
    result.Breakdown.BaseDamage  = DAMAGE_CONFIG.BaseDamage;
    result.Breakdown.StreakBonus = getStreakBonus(streak);

    // Bônus de velocidade (ambos acertaram e diferença > 200ms)
    if (defenderAct.HasAnswered && defenderAct.AnswerId === correctId) {
        var diff = Math.abs(attackerAct.AnswerTimestampMs - defenderAct.AnswerTimestampMs);
        if (diff > MATCH_CONFIG.SpeedBonusThresholdMs && attackerAct.AnswerTimestampMs < defenderAct.AnswerTimestampMs)
            result.Breakdown.SpeedBonus = DAMAGE_CONFIG.SpeedBonus;
    }

    // Power-up Bet
    if (attackerAct.ActivatedPowerUp === "Bet")
        result.Breakdown.PowerUpBonus = DAMAGE_CONFIG.BetBonus;

    // Shield do defensor
    if (isShielded(defenderState, defenderAct)) {
        result.WasShielded               = true;
        result.Breakdown.BaseDamage      = 0;
        result.Breakdown.SpeedBonus      = 0;
        result.Breakdown.StreakBonus     = 0;
        result.Breakdown.PowerUpBonus   = 0;
    }

    var bd = result.Breakdown;
    result.DamageDealt = bd.BaseDamage + bd.SpeedBonus + bd.StreakBonus + bd.PowerUpBonus;
    return result;
}

// ============================================================
// HELPERS
// ============================================================

function isShielded(state, action) {
    return action.ActivatedPowerUp === "SimpleShield"
        || action.ActivatedPowerUp === "DoubleShield"
        || state.DoubleShieldCharges > 0;
}

function updatePowerUpCharges(state, action) {
    if (isShielded(state, action) && state.DoubleShieldCharges > 0) state.DoubleShieldCharges--;
    if (action.ActivatedPowerUp === "DoubleShield") { state.DoubleShieldCharges = 1; state.HasUsedPowerUp = true; }
    else if (action.ActivatedPowerUp !== "None")    { state.HasUsedPowerUp = true; }
}

function bothPlayersAnswered(state) {
    return state.CurrentRoundState.Player1Action.HasAnswered
        && state.CurrentRoundState.Player2Action.HasAnswered;
}

function getPlayerAction(state, playerId) {
    return playerId === state.Player1Id
        ? state.CurrentRoundState.Player1Action
        : state.CurrentRoundState.Player2Action;
}

function getPlayerState(state, playerId) {
    return playerId === state.Player1Id ? state.Player1State : state.Player2State;
}

function determineWinnerByHP(state) {
    if (state.Player1State.HP > state.Player2State.HP) return state.Player1Id;
    if (state.Player2State.HP > state.Player1State.HP) return state.Player2Id;
    return null;
}

function buildRoundResponse(state, alreadyProcessed) {
    var round = state.CurrentRoundState;
    return {
        AlreadyProcessed: alreadyProcessed,
        Player1Result:    round.Player1Result,
        Player2Result:    round.Player2Result,
        Player1HP:        state.Player1State.HP,
        Player2HP:        state.Player2State.HP,
        IsMatchOver:      !state.IsActive,
        WinnerId:         state.WinnerId,
        EndReason:        state.EndReason || null
    };
}

function finalizeMatch(state, winnerId, reason) {
    state.IsActive  = false;
    state.WinnerId  = winnerId;
    state.EndReason = reason;
    state.Phase     = "MatchEnd";
    saveMatchState(state);
    removeFromActiveIndex(state.MatchId);
    updatePlayerStats(state, winnerId);
    return { status: "finalized", winnerId: winnerId };
}

function makePlayerState(playerId, equippedPowerUp) {
    return {
        PlayerId:               playerId,
        HP:                     MATCH_CONFIG.InitialHP,
        Streak:                 0,
        HasUsedPowerUp:         false,
        EquippedPowerUp:        equippedPowerUp || "None",
        DoubleShieldCharges:    0,
        IsConnected:            true,
        ConsecutiveMissedRounds: 0
    };
}

function makeAction(playerId) {
    return { PlayerId: playerId, AnswerId: null, AnswerTimestampMs: 0, HasAnswered: false, ActivatedPowerUp: "None" };
}

// ============================================================
// DADOS — implementar com seu sistema de perguntas/decks
// ============================================================

function getEquippedPowerUp(playerId) {
    var result = server.GetUserInternalData({ PlayFabId: playerId, Keys: ["EquippedPowerUp"] });
    if (result.Data && result.Data["EquippedPowerUp"])
        return result.Data["EquippedPowerUp"].Value || "None";
    return "None";
}

function buildQuestionPool(p1Id, p2Id) {
    // TODO: carregar os decks dos dois jogadores, combinar e embaralhar
    // Por ora usa IDs fixos para teste
    var pool = [];
    for (var i = 1; i <= MATCH_CONFIG.MaxRounds; i++) pool.push("question_" + i);
    return pool;
}

function getQuestionForRound(state, roundNumber) {
    var idx = roundNumber - 1;
    if (idx < 0 || idx >= state.QuestionPool.length) return null;
    return loadQuestion(state.QuestionPool[idx]);
}

function loadQuestion(questionId) {
    // TODO: buscar do Title Data ou Catalog usando server.GetTitleData / server.GetCatalogItems
    // Estrutura obrigatória do retorno:
    // { QuestionId, Text, ThemeId, ThemeName, Options:[{Id,Text}], CorrectOptionId }
    return {
        QuestionId:       questionId,
        Text:             "Pergunta de exemplo: " + questionId,
        ThemeId:          "tema_historia",
        ThemeName:        "Historia",
        Options:          [
            { Id: "a", Text: "Opcao A" },
            { Id: "b", Text: "Opcao B" },
            { Id: "c", Text: "Opcao C" },
            { Id: "d", Text: "Opcao D" }
        ],
        CorrectOptionId:  "a"
    };
}

function updatePlayerStats(state, winnerId) {
    // TODO: server.UpdatePlayerStatistics para wins/losses/ELO
}

