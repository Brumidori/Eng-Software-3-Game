// ============================================================
// BrainDuelCloudScript.js — PlayFab CloudScript V1
//
// COMO USAR:
//   1. Acesse: https://developer.playfab.com
//   2. Seu título → Build → CloudScript
//   3. Cole TODO este arquivo na aba de edição
//   4. Clique em "Save" e depois "Deploy to players"
// ============================================================


// ============================================================
// SEÇÃO 1 — ECONOMY
// ============================================================

handlers.EconomyAddCurrency = function (args, context) {
    var currencyCode = args && args.currencyCode ? args.currencyCode : null;
    var amount = args && args.amount ? parseInt(args.amount, 10) : 0;

    if (!currencyCode) return { success: false, error: "currencyCode is required" };
    if (!amount || amount <= 0) return { success: false, error: "amount must be > 0" };

    var addResult = server.AddUserVirtualCurrency({
        PlayFabId: currentPlayerId,
        VirtualCurrency: currencyCode,
        Amount: amount
    });

    return { success: true, operation: "add", currencyCode: currencyCode, balance: addResult.Balance };
};

handlers.EconomySubtractCurrency = function (args, context) {
    var currencyCode = args && args.currencyCode ? args.currencyCode : null;
    var amount = args && args.amount ? parseInt(args.amount, 10) : 0;

    if (!currencyCode) return { success: false, error: "currencyCode is required" };
    if (!amount || amount <= 0) return { success: false, error: "amount must be > 0" };

    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var currentBalance = inventory.VirtualCurrency && inventory.VirtualCurrency[currencyCode]
        ? inventory.VirtualCurrency[currencyCode] : 0;

    if (currentBalance < amount) {
        return { success: false, error: "insufficient balance", currencyCode: currencyCode, balance: currentBalance };
    }

    var subtractResult = server.SubtractUserVirtualCurrency({
        PlayFabId: currentPlayerId,
        VirtualCurrency: currencyCode,
        Amount: amount
    });

    return { success: true, operation: "subtract", currencyCode: currencyCode, balance: subtractResult.Balance };
};

handlers.EconomyGetBalance = function (args, context) {
    var currencyCode = args && args.currencyCode ? args.currencyCode : null;
    if (!currencyCode) return { success: false, error: "currencyCode is required" };

    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var balance = inventory.VirtualCurrency && inventory.VirtualCurrency[currencyCode]
        ? inventory.VirtualCurrency[currencyCode] : 0;

    return { success: true, operation: "getBalance", currencyCode: currencyCode, balance: balance };
};


// ============================================================
// SEÇÃO 2 — DECK ADMIN CRUD
// ============================================================

var DECK_INDEX_KEY         = "deck_index";
var ROLE_KEY               = "role";
var ADMIN_ROLE             = "admin";
var DEFAULT_CATALOG_VERSION = "mainCatalog";
var DEFAULT_STARTER_SKIN_ID = "skinDefault";

handlers.ValidatePlayerRole = function (args, context) {
    var roleResult = getCurrentPlayerRole();
    if (!roleResult.success) return roleResult;
    return { success: true, role: roleResult.role };
};

handlers.GrantStarterDecks = function (args, context) {
    try {
        var catalogVersion = args && args.catalogVersion ? String(args.catalogVersion) : DEFAULT_CATALOG_VERSION;
        var catalogResult  = server.GetCatalogItems({ CatalogVersion: catalogVersion });
        var eligibleItemIds = findStarterDeckItemIds(catalogResult);
        addUniqueItemId(eligibleItemIds, DEFAULT_STARTER_SKIN_ID);

        if (eligibleItemIds.length === 0)
            return fail("no starter decks configured for catalog version: " + catalogVersion);

        var inventoryResult = server.GetUserInventory({ PlayFabId: currentPlayerId });
        var ownedItemIds    = buildOwnedItemIdMap(inventoryResult);
        var missingItemIds  = [];

        for (var i = 0; i < eligibleItemIds.length; i++) {
            if (!ownedItemIds[eligibleItemIds[i]]) missingItemIds.push(eligibleItemIds[i]);
        }

        if (missingItemIds.length === 0)
            return { success: true, alreadyGranted: true, catalogVersion: catalogVersion, grantedItemIds: eligibleItemIds };

        server.GrantItemsToUser({
            PlayFabId:      currentPlayerId,
            CatalogVersion: catalogVersion,
            ItemIds:        missingItemIds,
            Annotation:     "Starter deck grant on registration"
        });

        return { success: true, alreadyGranted: false, catalogVersion: catalogVersion, grantedItemIds: missingItemIds };
    } catch (error) {
        return fail("unexpected error while granting starter decks", error);
    }
};

handlers.DeckAdminListCatalog = function (args) {
    var guard = requireAdmin();
    if (!guard.success) return guard;
    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;
    return { success: true, deckIndex: indexResult.deckIndex };
};

handlers.DeckAdminToggleDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var key = args && args.key ? String(args.key) : "";
    if (!key) return fail("key is required");

    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;

    var deckIndex = indexResult.deckIndex;
    var category  = findCategoryByKey(deckIndex, key);
    if (!category) return fail("key not found in deck_index: " + key);

    category.ativo   = category.ativo === false ? true : false;
    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) return saveResult;

    return { success: true, operation: "toggle", key: key, ativo: category.ativo, versao: deckIndex.versao };
};

handlers.DeckAdminGetDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var key = args && args.key ? String(args.key) : "";
    if (!key) return fail("key is required");

    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;

    var category = findCategoryByKey(indexResult.deckIndex, key);
    if (!category) return fail("deck key not found in deck_index");

    var titleData = server.GetTitleData({ Keys: [key] });
    var rawDeck   = titleData && titleData.Data ? titleData.Data[key] : null;
    if (!rawDeck) return fail("deck not found in TitleData for key: " + key);

    var parsed = safeJsonParse(rawDeck);
    if (!parsed.success) return fail("invalid deck JSON in TitleData: " + key, parsed.error);

    var validation = validateDeckPayload(parsed.value, category.nome, key);
    if (!validation.success) return fail("deck payload is invalid", validation.errors);

    return { success: true, operation: "get", key: key, nome: category.nome, deck: parsed.value, deckJson: JSON.stringify(parsed.value), validationWarnings: validation.warnings };
};

handlers.DeckAdminCreateDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var nome        = args && args.nome ? String(args.nome) : "";
    var key         = args && args.key  ? String(args.key)  : "";
    var deckPayload = args ? args.deck : null;

    if (!nome) return fail("nome is required");
    if (!key)  return fail("key is required");

    var keyValidation = validateDeckKey(key);
    if (!keyValidation.success) return keyValidation;

    var validation = validateDeckPayload(deckPayload, nome, key);
    if (!validation.success) return fail("deck validation failed", validation.errors);

    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;

    var deckIndex = indexResult.deckIndex;
    if (findCategoryByKey(deckIndex, key))  return fail("key already exists in deck_index");
    if (findCategoryByName(deckIndex, nome)) return fail("nome already exists in deck_index");

    var normalizedDeck = normalizeDeckPayload(deckPayload, nome);
    server.SetTitleData({ Key: key, Value: JSON.stringify(normalizedDeck) });

    deckIndex.categorias.push({ nome: nome, key: key });
    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) return saveResult;

    return { success: true, operation: "create", key: key, nome: nome, versao: deckIndex.versao, warnings: validation.warnings };
};

handlers.DeckAdminUpdateDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var key         = args && args.key  ? String(args.key)  : "";
    var novoNome    = args && args.nome ? String(args.nome) : null;
    var deckPayload = args && args.deck ? args.deck : null;

    if (!key) return fail("key is required");

    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;

    var deckIndex = indexResult.deckIndex;
    var category  = findCategoryByKey(deckIndex, key);
    if (!category) return fail("key not found in deck_index");
    if (!novoNome && !deckPayload) return fail("nothing to update: send nome and/or deck");

    if (novoNome) {
        var duplicateByName = findCategoryByName(deckIndex, novoNome);
        if (duplicateByName && duplicateByName.key !== key) return fail("nome already exists in another category");
    }

    var targetName = novoNome || category.nome;
    var validation = null;

    if (deckPayload) {
        validation = validateDeckPayload(deckPayload, targetName, key);
        if (!validation.success) return fail("deck validation failed", validation.errors);
        var normalizedDeck = normalizeDeckPayload(deckPayload, targetName);
        server.SetTitleData({ Key: key, Value: JSON.stringify(normalizedDeck) });
    }

    if (novoNome) category.nome = novoNome;
    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) return saveResult;

    return { success: true, operation: "update", key: key, nome: category.nome, versao: deckIndex.versao, warnings: validation ? validation.warnings : [] };
};

handlers.DeckAdminDeleteDeck = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var key              = args && args.key ? String(args.key) : "";
    var clearDeckContent = !args || args.clearDeckContent !== false;
    if (!key) return fail("key is required");

    var indexResult = loadDeckIndex();
    if (!indexResult.success) return indexResult;

    var deckIndex = indexResult.deckIndex;
    var category  = findCategoryByKey(deckIndex, key);
    if (!category) return fail("key not found in deck_index");

    var kept = [];
    for (var i = 0; i < deckIndex.categorias.length; i++) {
        if (deckIndex.categorias[i].key !== key) kept.push(deckIndex.categorias[i]);
    }
    deckIndex.categorias = kept;
    deckIndex.versao = toPositiveInt(deckIndex.versao, 1) + 1;

    var saveResult = saveDeckIndex(deckIndex);
    if (!saveResult.success) return saveResult;

    var keyDeleteResult = { success: true, keyDeleted: false, mode: "skipped" };
    if (clearDeckContent) {
        keyDeleteResult = deleteDeckTitleDataKey(key);
        if (!keyDeleteResult.success) return fail("deck removed from deck_index but failed to delete TitleData key", keyDeleteResult.error);
    }

    return { success: true, operation: "delete", key: key, removedCategoryName: category.nome, versao: deckIndex.versao, contentCleared: clearDeckContent, keyDeleted: keyDeleteResult.keyDeleted, keyDeleteMode: keyDeleteResult.mode };
};

handlers.DeckAdminValidateDeckPayload = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) return guard;

    var nome        = args && args.nome ? String(args.nome) : "";
    var key         = args && args.key  ? String(args.key)  : "";
    var deckPayload = args ? args.deck : null;

    var result = validateDeckPayload(deckPayload, nome, key);
    return { success: result.success, errors: result.errors, warnings: result.warnings };
};

// --- Deck Admin helpers ---

function requireAdmin() {
    var roleResult = getCurrentPlayerRole();
    if (!roleResult.success) return roleResult;
    if (String(roleResult.role).toLowerCase() !== ADMIN_ROLE) return fail("forbidden: admin role required");
    return { success: true };
}

function getCurrentPlayerRole() {
    try {
        var userInternal = server.GetUserInternalData({ PlayFabId: currentPlayerId, Keys: [ROLE_KEY] });
        if (!userInternal || !userInternal.Data || !userInternal.Data[ROLE_KEY] || !userInternal.Data[ROLE_KEY].Value)
            return fail("role not found for current user");
        return { success: true, role: String(userInternal.Data[ROLE_KEY].Value) };
    } catch (error) {
        return fail("failed to get user role", error);
    }
}

// Carrega e valida o deck_index do TitleData.
// Retorna { success, deckIndex } — usado tanto pelo Admin quanto pelo match.
function loadDeckIndex() {
    var titleData = server.GetTitleData({ Keys: [DECK_INDEX_KEY] });
    var raw = titleData && titleData.Data ? titleData.Data[DECK_INDEX_KEY] : null;
    if (!raw) return fail("deck_index not found in TitleData");

    var parsed = safeJsonParse(raw);
    if (!parsed.success) return fail("invalid JSON in deck_index", parsed.error);

    var deckIndex = parsed.value;
    if (!deckIndex || typeof deckIndex !== "object") return fail("deck_index payload must be an object");
    if (!Array.isArray(deckIndex.categorias)) return fail("deck_index.categorias must be an array");
    if (typeof deckIndex.versao !== "number") deckIndex.versao = 1;

    return { success: true, deckIndex: deckIndex };
}

function findStarterDeckItemIds(catalogResult) {
    if (!catalogResult || !Array.isArray(catalogResult.Catalog)) return [];

    // Primeira passagem: itens marcados com is_starter=true no CustomData
    var marked = [];
    var all    = [];
    var seen   = {};

    for (var i = 0; i < catalogResult.Catalog.length; i++) {
        var item   = catalogResult.Catalog[i];
        var itemId = item && String(item.ItemId || "").trim();
        if (!itemId || seen[itemId]) continue;
        if (String(itemId).toLowerCase().indexOf("deck") !== 0) continue;
        seen[itemId] = true;
        all.push(itemId);
        if (isStarterDeckCatalogItem(item)) marked.push(itemId);
    }

    // Se algum item tiver is_starter=true, usa apenas eles.
    // Caso contrário, concede todos os decks do catálogo (fallback automático).
    return marked.length > 0 ? marked : all;
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
    if (!item || !isNonEmptyString(item.ItemId)) return false;
    if (String(item.ItemId).toLowerCase().indexOf("deck") !== 0) return false;
    if (!isNonEmptyString(item.CustomData)) return false;
    var parsed = safeJsonParse(item.CustomData);
    if (!parsed.success || !parsed.value || typeof parsed.value !== "object") return false;
    return isTruthyStarterFlag(parsed.value.is_starter);
}

function isTruthyStarterFlag(value) {
    return value === true || String(value).toLowerCase() === "true";
}

function buildOwnedItemIdMap(inventoryResult) {
    var owned = {};
    if (!inventoryResult || !Array.isArray(inventoryResult.Inventory)) return owned;
    for (var i = 0; i < inventoryResult.Inventory.length; i++) {
        var item = inventoryResult.Inventory[i];
        if (item && isNonEmptyString(item.ItemId)) owned[String(item.ItemId)] = true;
    }
    return owned;
}

function saveDeckIndex(deckIndex) {
    if (!deckIndex || typeof deckIndex !== "object") return fail("deckIndex is invalid");
    if (!Array.isArray(deckIndex.categorias)) return fail("deckIndex.categorias must be array");
    server.SetTitleData({ Key: DECK_INDEX_KEY, Value: JSON.stringify(deckIndex) });
    return { success: true };
}

function deleteDeckTitleDataKey(key) {
    if (!isNonEmptyString(key)) return fail("key is required for TitleData deletion");
    try {
        if (server && typeof server.DeleteTitleData === "function") {
            server.DeleteTitleData({ Key: key });
            return { success: true, keyDeleted: true, mode: "DeleteTitleData" };
        }
        server.SetTitleData({ Key: key, Value: null });
        var check = server.GetTitleData({ Keys: [key] });
        var hasKey = check && check.Data && Object.prototype.hasOwnProperty.call(check.Data, key);
        var consideredDeleted = !hasKey || check.Data[key] === null || check.Data[key] === "";
        if (!consideredDeleted) return fail("DeleteTitleData unavailable and fallback did not remove key");
        return { success: true, keyDeleted: true, mode: "SetTitleData(null)" };
    } catch (error) {
        return fail("failed deleting deck TitleData key", error);
    }
}

function validateDeckKey(key) {
    if (!key) return fail("key is required");
    return { success: true };
}

function validateDeckPayload(payload, categoriaNome, categoriaKey) {
    var errors = []; var warnings = [];
    if (!payload || typeof payload !== "object") { errors.push("deck payload must be an object"); return resultValidation(errors, warnings); }
    if (!isNonEmptyString(payload.deck_id)) errors.push("deck_id is required");
    if (!isNonEmptyString(payload.theme))   errors.push("theme is required");
    if (!Array.isArray(payload.questions))  { errors.push("questions must be an array"); return resultValidation(errors, warnings); }
    if (payload.questions.length === 0)     { errors.push("questions must have at least one entry"); return resultValidation(errors, warnings); }

    var ids = {};
    for (var i = 0; i < payload.questions.length; i++) {
        var q = payload.questions[i];
        var label = "questions[" + i + "]";
        if (!q || typeof q !== "object") { errors.push(label + " must be an object"); continue; }
        if (!isNonEmptyString(q.id))      errors.push(label + ".id is required");
        else if (ids[q.id])               errors.push(label + ".id is duplicated: " + q.id);
        else                              ids[q.id] = true;
        if (!isNonEmptyString(q.text))    errors.push(label + ".text is required");
        if (!Array.isArray(q.options))    { errors.push(label + ".options must be an array"); continue; }
        if (q.options.length < 4)         errors.push(label + ".options must have at least 4 entries");
        var correctCount = 0;
        for (var j = 0; j < q.options.length; j++) {
            var opt = q.options[j];
            var optLabel = label + ".options[" + j + "]";
            if (!opt || typeof opt !== "object") { errors.push(optLabel + " must be an object"); continue; }
            if (!isNonEmptyString(opt.text)) errors.push(optLabel + ".text must be a non-empty string");
            if (typeof opt.is_correct !== "boolean") errors.push(optLabel + ".is_correct must be boolean");
            else if (opt.is_correct) correctCount++;
        }
        if (correctCount !== 1) errors.push(label + " must have exactly one option with is_correct=true");
        if (typeof q.time_limit !== "number" || Math.floor(q.time_limit) !== q.time_limit || q.time_limit < 1)
            errors.push(label + ".time_limit must be an integer >= 1");
    }
    if (isNonEmptyString(payload.theme) && isNonEmptyString(categoriaNome) && payload.theme !== categoriaNome)
        warnings.push("theme differs from category name: " + payload.theme);
    if (isNonEmptyString(categoriaKey)) {
        var keyResult = validateDeckKey(categoriaKey);
        if (!keyResult.success) errors.push(keyResult.error);
    }
    return resultValidation(errors, warnings);
}

function normalizeDeckPayload(payload, categoriaNome) {
    var out = {
        deck_id: String(payload.deck_id).trim(),
        theme:   isNonEmptyString(payload.theme) ? String(payload.theme).trim() : String(categoriaNome || "").trim(),
        questions: []
    };
    for (var i = 0; i < payload.questions.length; i++) {
        var q = payload.questions[i];
        var normalizedOptions = [];
        for (var j = 0; j < q.options.length; j++) {
            normalizedOptions.push({ text: String(q.options[j].text).trim(), is_correct: q.options[j].is_correct === true });
        }
        out.questions.push({ id: String(q.id).trim(), text: String(q.text).trim(), options: normalizedOptions, time_limit: parseInt(q.time_limit, 10) });
    }
    return out;
}

function findCategoryByKey(deckIndex, key) {
    if (!deckIndex || !Array.isArray(deckIndex.categorias)) return null;
    for (var i = 0; i < deckIndex.categorias.length; i++) {
        if (deckIndex.categorias[i] && deckIndex.categorias[i].key === key) return deckIndex.categorias[i];
    }
    return null;
}

function findCategoryByName(deckIndex, nome) {
    if (!deckIndex || !Array.isArray(deckIndex.categorias)) return null;
    for (var i = 0; i < deckIndex.categorias.length; i++) {
        if (deckIndex.categorias[i] && deckIndex.categorias[i].nome === nome) return deckIndex.categorias[i];
    }
    return null;
}

function safeJsonParse(raw) {
    try { return { success: true, value: JSON.parse(raw) }; }
    catch (error) { return fail("json parse error", error); }
}

function isNonEmptyString(value) {
    return typeof value === "string" && value.trim().length > 0;
}

function toPositiveInt(value, fallback) {
    if (typeof value === "number" && value >= 1 && Math.floor(value) === value) return value;
    return fallback;
}

function resultValidation(errors, warnings) {
    return { success: errors.length === 0, errors: errors, warnings: warnings };
}

function fail(message, details) {
    var result = { success: false, error: message };
    if (typeof details !== "undefined" && details !== null) result.details = details;
    return result;
}


// ============================================================
// SEÇÃO 3 — PARTIDA (match authoritative)
// ============================================================

var MATCH_CONFIG = {
    InitialHP:               100,
    MaxRounds:               20,
    ThemePhaseDurationMs:    5000,
    QuestionPhaseDurationMs: 15000,
    SpeedBonusThresholdMs:   200,
    AfkRoundLimit:           3,
    ReconnectWindowMs:       30000
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

// --- Storage ---

function saveMatchState(state) {
    server.UpdateSharedGroupData({ SharedGroupId: state.MatchId, Data: { "state": JSON.stringify(state) } });
}

function loadMatchState(matchId) {
    try {
        var result = server.GetSharedGroupData({ SharedGroupId: matchId, Keys: ["state"] });
        var json   = result.Data && result.Data["state"] ? result.Data["state"].Value : null;
        return json ? JSON.parse(json) : null;
    } catch (e) {
        return null;
    }
}

function deleteMatchState(matchId) {
    try {
        server.DeleteSharedGroup({ SharedGroupId: matchId });
    } catch (e) { }
    removeFromActiveIndex(matchId);
}

function loadActiveIndex() {
    var result = server.GetTitleInternalData({ Keys: ["active_matches_index"] });
    var json   = result.Data["active_matches_index"];
    return json ? JSON.parse(json) : [];
}

function saveActiveIndex(ids) {
    server.SetTitleInternalData({ Key: "active_matches_index", Value: JSON.stringify(ids) });
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

// --- Handlers de partida ---

handlers.CreateMatch = function (args) {
    
    var matchId = args.matchId;
    // currentPlayerId é sempre o PlayFabId clássico do chamador — usa como fallback se player1Id vier vazio
    var player1Id = isNonEmptyString(args.player1Id) ? args.player1Id : currentPlayerId;
    var player2Id = args.player2Id;

    var existing = loadMatchState(matchId);
    if (existing && existing.IsActive) {
        // Registra P2 se ainda estiver vazio e o chamador for diferente de P1
        if (!isNonEmptyString(existing.Player2Id)
                && isNonEmptyString(currentPlayerId)
                && currentPlayerId !== existing.Player1Id) {
            existing.Player2Id = currentPlayerId;
            // Atualiza Player2State com dados reais do jogador
            var p2Info    = getPlayerDisplayInfo(currentPlayerId);
            var p2PowerUp = getEquippedPowerUp(currentPlayerId);
            if (existing.Player2State) {
                existing.Player2State.PlayerId        = currentPlayerId;
                existing.Player2State.DisplayName     = p2Info.displayName || "";
                existing.Player2State.Level           = p2Info.level       || 1;
                existing.Player2State.EquippedPowerUp = p2PowerUp          || "None";
                existing.Player2State.AvatarId        = p2Info.avatarId    || "skinDefault";
            }
            // Atualiza ação de P2 na rodada atual, se existir
            if (existing.CurrentRoundState && existing.CurrentRoundState.Player2Action)
                existing.CurrentRoundState.Player2Action.PlayerId = currentPlayerId;
            saveMatchState(existing);
        }
        return { success: true, matchId: matchId, networkDescriptor: existing.PartyNetworkDescriptor };
    }

    var p1PowerUp    = getEquippedPowerUp(player1Id);
    var p2PowerUp    = getEquippedPowerUp(player2Id);
    var p1Info       = getPlayerDisplayInfo(player1Id);
    var p2Info       = getPlayerDisplayInfo(player2Id);
    var questionPool = buildQuestionPool(player1Id, player2Id);

    var state = {
        MatchId:               matchId,
        Player1Id:             player1Id,
        Player2Id:             player2Id,
        Player1State:          makePlayerState(player1Id, p1Info.displayName, p1Info.level, p1PowerUp, p1Info.avatarId),
        Player2State:          makePlayerState(player2Id, p2Info.displayName, p2Info.level, p2PowerUp, p2Info.avatarId),
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

    try {
        server.CreateSharedGroup({ SharedGroupId: matchId });
    } catch (e) {
        // Ignora caso o grupo já exista
    }

    saveMatchState(state);
    addToActiveIndex(matchId);
    return { success: true, matchId: matchId, networkDescriptor: state.PartyNetworkDescriptor };
};

handlers.StartNextRound = function (args) {
    var state = loadMatchState(args.matchId);
    if (!state || !state.IsActive)            return { error: "Match inativo" };
    if (args.roundNumber > MATCH_CONFIG.MaxRounds) return finalizeMatch(state, determineWinnerByHP(state), "RoundsOver");

    // Idempotente: rodada ja iniciada — retorna os dados existentes ao segundo jogador.
    // O timestamp do primeiro chamador e usado por ambos, garantindo sincronizacao de timer.
    if (state.CurrentRound >= args.roundNumber) {
        var existingRound = state.CurrentRoundState;
        var existingQ     = existingRound ? loadQuestion(state, existingRound.QuestionId) : null;
        return {
            status:             "already_started",
            roundNumber:        state.CurrentRound,
            themeId:            existingRound ? existingRound.ThemeId   : "",
            themeName:          existingRound ? existingRound.ThemeName : "",
            serverTimestampMs:  state.PhaseStartTimestampMs,
            themeDurationMs:    MATCH_CONFIG.ThemePhaseDurationMs,
            questionDurationMs: MATCH_CONFIG.QuestionPhaseDurationMs,
            question:           existingQ ? { QuestionId: existingQ.QuestionId, QuestionText: existingQ.Text, Answers: existingQ.Options } : null,
            player1Id:          state.Player1Id,
            player2Id:          state.Player2Id,
            player1State:       state.Player1State,
            player2State:       state.Player2State
        };
    }

    // Bug 2 fix: Protecao contra Lost Update (Eventual Consistency)
    // Se o cliente esta pedindo a rodada N, a rodada N-1 TEM que estar processada!
    // Se a rodada atual no state for menor que N-1, ou for N-1 mas IsProcessed == false,
    // significa que carregamos uma replica STALE do banco de dados (que ainda nao tem o ProcessRound da rodada anterior salvo).
    // Se salvarmos esse state stale com CurrentRound=N, iremos DESTRUIR o HP calculado na rodada N-1!
    if (args.roundNumber > 1) {
        var isStale = false;
        if (state.CurrentRound < args.roundNumber - 1) {
            isStale = true;
        } else if (state.CurrentRound === args.roundNumber - 1 && state.CurrentRoundState && !state.CurrentRoundState.IsProcessed) {
            isStale = true;
        }

        if (isStale) {
            // Retornamos "waiting" para forcar o cliente a retentar o StartNextRound
            // em alguns milissegundos, quando o cache do PlayFab ja devera estar atualizado.
            return { status: "waiting", reason: "stale_state_lost_update_protection" };
        }
    }


    // Primeiro chamador: cria a rodada com timestamp autoritativo.
    // IMPORTANTE: nao ha write intermediario aqui — apenas UM write final com CurrentRound=N.
    // O gate anterior (NextRoundConfirmedBy) fazia um write com CurrentRound=N-1 antes do write
    // final com CurrentRound=N, o que deixava replicas desatualizadas retornando N-1 ao SubmitAnswer.
    // Sem o gate, ambos os clientes recebem o mesmo PhaseStartTimestampMs (o do primeiro chamador),
    // garantindo sincronizacao de timer sem writes intermediarios.
    var question = getQuestionForRound(state, args.roundNumber);
    if (!question) return { error: "Pergunta nao encontrada para rodada " + args.roundNumber };

    state.CurrentRound          = args.roundNumber;
    state.Phase                 = "ThemeAndPowerUp";
    state.PhaseStartTimestampMs = Date.now();
    state.NextRoundConfirmedBy  = null;  // limpa campos legados do gate removido
    state.NextRoundConfirmedAt  = null;
    state.CurrentRoundState     = {
        RoundNumber:     args.roundNumber,
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
        status:             "ok",
        roundNumber:        args.roundNumber,
        themeId:            question.ThemeId,
        themeName:          question.ThemeName,
        serverTimestampMs:  state.PhaseStartTimestampMs,
        themeDurationMs:    MATCH_CONFIG.ThemePhaseDurationMs,
        questionDurationMs: MATCH_CONFIG.QuestionPhaseDurationMs,
        question:           { QuestionId: question.QuestionId, QuestionText: question.Text, Answers: question.Options },
        player1Id:          state.Player1Id,
        player2Id:          state.Player2Id,
        player1State:       state.Player1State,
        player2State:       state.Player2State
    };
};

// Barreira de sincronização: ambos os jogadores devem chamar PlayerReady antes de
// começar a rodada 1. O segundo jogador a confirmar cria a rodada com um timestamp
// autoritativo único, garantindo que os dois timers partam do mesmo ponto.
handlers.PlayerReady = function(args) {
    var state = loadMatchState(args.matchId);
    if (!state || !state.IsActive) return { error: "Match inativo" };

    // Idempotente: rodada 1 já iniciada (inclui retries e clientes legados que chamam StartNextRound diretamente)
    if (state.CurrentRound >= 1 && state.CurrentRoundState) {
        var idempQ = loadQuestion(state, state.CurrentRoundState.QuestionId);
        return {
            status:             "ready",
            roundNumber:        state.CurrentRound,
            themeId:            state.CurrentRoundState.ThemeId,
            themeName:          state.CurrentRoundState.ThemeName,
            serverTimestampMs:  state.PhaseStartTimestampMs,
            themeDurationMs:    MATCH_CONFIG.ThemePhaseDurationMs,
            questionDurationMs: MATCH_CONFIG.QuestionPhaseDurationMs,
            question:           idempQ ? { QuestionId: idempQ.QuestionId, QuestionText: idempQ.Text, Answers: idempQ.Options } : null,
            questionPool:       buildClientPool(state.QuestionPool),
            player1Id:          state.Player1Id,
            player2Id:          state.Player2Id,
            player1State:       state.Player1State,
            player2State:       state.Player2State
        };
    }

    // Careful merge: recarrega estado fresco antes de salvar IsReady para não
    // sobrescrever o IsReady do outro jogador em chamadas concorrentes.
    var ps = getPlayerState(state, currentPlayerId);
    if (ps && !ps.IsReady) {
        var freshState = loadMatchState(args.matchId);
        if (freshState && freshState.CurrentRound < 1) {
            var freshPs = getPlayerState(freshState, currentPlayerId);
            if (freshPs) {
                freshPs.IsReady = true;
                saveMatchState(freshState);
                state = freshState;
            }
        } else if (freshState) {
            state = freshState;
        }
    }

    var p1Ready = state.Player1State && state.Player1State.IsReady;
    var p2Ready = state.Player2State && state.Player2State.IsReady;

    if (!p1Ready || !p2Ready) {
        return { status: "waiting", readyCount: (p1Ready ? 1 : 0) + (p2Ready ? 1 : 0) };
    }

    // Ambos prontos — inicia rodada 1 com timestamp único e autoritativo
    var question = getQuestionForRound(state, 1);
    if (!question) return { error: "Pergunta não encontrada para rodada 1" };

    state.CurrentRound          = 1;
    state.Phase                 = "ThemeAndPowerUp";
    state.PhaseStartTimestampMs = Date.now();
    state.CurrentRoundState     = {
        RoundNumber:     1,
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
        status:             "ready",
        roundNumber:        1,
        themeId:            question.ThemeId,
        themeName:          question.ThemeName,
        serverTimestampMs:  state.PhaseStartTimestampMs,
        themeDurationMs:    MATCH_CONFIG.ThemePhaseDurationMs,
        questionDurationMs: MATCH_CONFIG.QuestionPhaseDurationMs,
        question:           { QuestionId: question.QuestionId, QuestionText: question.Text, Answers: question.Options },
        questionPool:       buildClientPool(state.QuestionPool),
        player1Id:          state.Player1Id,
        player2Id:          state.Player2Id,
        player1State:       state.Player1State,
        player2State:       state.Player2State
    };
};

handlers.StartQuestion = function (args) {
    
    var state = loadMatchState(args.matchId);
    if (!state) return { status: "ignored", reason: "match_not_found", matchId: args.matchId };
    if (state.CurrentRound !== args.roundNumber)
        return { status: "ignored", reason: "round_mismatch", serverRound: state.CurrentRound, clientRound: args.roundNumber, phase: state.Phase };

    // Idempotente: aceita tanto ThemeAndPowerUp quanto Question (segundo jogador a chamar)
    if (state.Phase !== "ThemeAndPowerUp" && state.Phase !== "Question")
        return { status: "wrong_phase" };

    if (state.Phase === "ThemeAndPowerUp") {
        state.Phase                 = "Question";
        state.PhaseStartTimestampMs = Date.now();
        try {
            saveMatchState(state);
        } catch (saveErr) {
            // Se a escrita falhou, recarrega para saber se o outro jogador já salvou.
            try {
                var reloaded = loadMatchState(args.matchId);
                if (reloaded && reloaded.Phase === "Question") {
                    // Outro jogador já fez a transição — usa o timestamp dele
                    state.PhaseStartTimestampMs = reloaded.PhaseStartTimestampMs;
                } else {
                    // Falha real: informa o cliente para que ele retente StartQuestion.
                    // Sem isso o cliente entraria em Question com o servidor em ThemeAndPowerUp.
                    return { status: "save_error", reason: "phase_transition_failed" };
                }
            } catch (e2) {
                return { status: "save_error", reason: "reload_failed" };
            }
        }
    }

    var question = loadQuestion(state, state.CurrentRoundState.QuestionId);
    if (!question) return { error: "Pergunta nao encontrada" };

    // Se o jogador que chamou ativou EliminateTwo, calcula 2 índices errados para eliminar
    var playerAction      = getPlayerAction(state, currentPlayerId);
    var eliminatedIndices = null;
    if (playerAction && playerAction.ActivatedPowerUp === "EliminateTwo")
        eliminatedIndices = calcularIndicesEliminados(question.Options, question.CorrectOptionId);

    // PascalCase para corresponder exatamente ao modelo C# QuestionRevealPayload
    return {
        QuestionId:        question.QuestionId,
        QuestionText:      question.Text,
        Answers:           question.Options,
        ServerTimestampMs: state.PhaseStartTimestampMs,
        DurationMs:        MATCH_CONFIG.QuestionPhaseDurationMs,
        EliminatedIndices: eliminatedIndices
    };
};

handlers.SubmitAnswer = function (args) {

    var playerId = currentPlayerId;
    var state    = loadMatchState(args.matchId);

    if (!state)            return { error: "Match inativo", reason: "not_found", matchId: args.matchId };
    if (!state.IsActive)   return { error: "Match inativo", reason: "ended", endReason: state.EndReason, winnerId: state.WinnerId };
    // Se o servidor ainda não avançou para a rodada do cliente (eventual consistency),
    // tenta uma leitura fresca antes de rejeitar.
    if (state.CurrentRound !== args.roundNumber) {
        var freshCheck = loadMatchState(args.matchId);
        if (freshCheck && freshCheck.CurrentRound === args.roundNumber) {
            state = freshCheck;
        } else if (freshCheck && freshCheck.CurrentRound > args.roundNumber
                   && freshCheck.LastProcessedRound >= args.roundNumber) {
            // A rodada já foi processada e o servidor avançou: retorna o resultado
            // como "already_processed" para que o cliente possa se ressincronizar.
            // Isso evita que o cliente fique preso ao receber wrong_round quando
            // um retry de ProcessRound chegou tarde e o servidor já está na próxima rodada.
            return {
                status:      "already_processed",
                serverRound: freshCheck.CurrentRound,
                clientRound: args.roundNumber,
                roundResult: freshCheck.CurrentRoundState && freshCheck.CurrentRoundState.IsProcessed
                    ? buildRoundResponse(freshCheck, true)
                    : null
            };
        } else {
            return { status: "wrong_round", serverRound: (freshCheck || state).CurrentRound, clientRound: args.roundNumber };
        }
    }
    // A checagem de Phase foi removida: StartQuestion não é mais chamado e o servidor
    // permanece em "ThemeAndPowerUp" durante toda a rodada. O controle de timing
    // é feito pelo cliente (que só envia SubmitAnswer depois do timer de pergunta iniciar)
    // e pelo ProcessRound (que valida o elapsed antes de processar).

    var action = getPlayerAction(state, playerId);
    if (action.HasAnswered) return { status: "already_answered" };

    // Recarrega estado fresco antes de salvar para não sobrescrever a resposta
    // concorrente do outro jogador (race condition em respostas simultâneas).
    var stateParaSalvar = loadMatchState(args.matchId);
    if (stateParaSalvar && stateParaSalvar.CurrentRound === args.roundNumber
            && !stateParaSalvar.CurrentRoundState.IsProcessed) {
        var actionFresco = getPlayerAction(stateParaSalvar, playerId);
        if (actionFresco.HasAnswered) return { status: "already_answered" };
        actionFresco.AnswerId          = args.answerId;
        actionFresco.AnswerTimestampMs = args.clientTimestampMs || Date.now();
        actionFresco.HasAnswered       = true;
        saveMatchState(stateParaSalvar);
        state = stateParaSalvar;
    } else {
        action.AnswerId          = args.answerId;
        action.AnswerTimestampMs = args.clientTimestampMs || Date.now();
        action.HasAnswered       = true;
        saveMatchState(state);
    }

    // Não processa imediatamente: ProcessRound (polling) é o único ponto de processamento.
    // Evita a race entre SubmitAnswer e ProcessRound que causava double-processing e
    // computava respostas tardias como NotAnswered.
    return { status: "ok" };
};

handlers.ActivatePowerUp = function (args) {
    
    var playerId = currentPlayerId;
    var state    = loadMatchState(args.matchId);

    if (!state || !state.IsActive)               return { error: "Match inativo" };
    if (state.CurrentRound !== args.roundNumber)  return { status: "wrong_round" };
    if (state.Phase !== "ThemeAndPowerUp")        return { error: "Fora da janela de power-up" };

    var ps     = getPlayerState(state, playerId);
    var action = getPlayerAction(state, playerId);

    if (ps.HasUsedPowerUp) return { error: "Power-up ja usado nesta partida" };
    // Qualquer power-up do inventário pode ser ativado — basta tê-lo.
    action.ActivatedPowerUp = args.powerUp;
    saveMatchState(state);

    // EliminateTwo: calcula quais índices eliminar e devolve ao cliente.
    // Antes ficava em StartQuestion — movido aqui para eliminar aquela chamada.
    var eliminatedIndices = null;
    if (String(args.powerUp).toLowerCase() === "eliminatetwo") {
        var qElim = loadQuestion(state, state.CurrentRoundState.QuestionId);
        if (qElim) eliminatedIndices = calcularIndicesEliminados(qElim.Options, qElim.CorrectOptionId);
    }
    return { status: "ok", eliminatedIndices: eliminatedIndices };
};

handlers.ProcessRound = function (args) {
    try {
        var state = loadMatchState(args.matchId);
        if (!state) return { error: "Match nao encontrado" };
        
        if (!state.IsActive) {
            // Se a partida encerrou nesta mesma rodada, ou se foi encerrada abruptamente (abandono),
            // o cliente tem direito de ler o resultado final para disparar a tela de vitoria/derrota.
            var endedNormally = (state.CurrentRound === args.roundNumber && state.CurrentRoundState && state.CurrentRoundState.IsProcessed);
            var endedAbruptly = (state.EndReason === 2 || state.EndReason === 3); // Abandonment or Disconnect

            if (endedNormally || endedAbruptly) {
                return buildRoundResponse(state, true);
            }
            return { error: "Match inativo" };
        }

        if (state.CurrentRound !== args.roundNumber)  return { status: "wrong_round" };
        if (state.CurrentRoundState.IsProcessed)      return buildRoundResponse(state, true);

        // Calcula o início da fase de pergunta:
        // - Se o servidor já está em "Question" (StartQuestion foi chamado e resetou
        //   PhaseStartTimestampMs para o momento da transição), o offset do tema não
        //   deve ser adicionado — PhaseStartTimestampMs já é o início da pergunta.
        // - Caso contrário (fase ainda em "ThemeAndPowerUp", fluxo normal com pergunta
        //   embutida), adiciona ThemePhaseDurationMs para obter o início correto.
        var questionPhaseStart = state.Phase === "Question"
            ? state.PhaseStartTimestampMs
            : state.PhaseStartTimestampMs + MATCH_CONFIG.ThemePhaseDurationMs;
        var elapsed = Date.now() - questionPhaseStart;
        // Grace period: dá 1.5s extras para SubmitAnswer tardios chegarem ao servidor
        // antes de processar. Resolve a race onde o ProcessRound do timer do cliente
        // chegava antes do SubmitAnswer do jogador (que clicou nos últimos segundos).
        var graceMs = 1500;
        if (elapsed < MATCH_CONFIG.QuestionPhaseDurationMs + graceMs)
            return { status: "pending" };

        return processRoundInternal(state);

    } catch (e) {
        var info = e && e.apiErrorInfo ? e.apiErrorInfo : null;
        return {
            error:    "ProcessRound_api_error",
            apiName:  info ? info.api : null,
            apiError: info ? info.apiError : String(e)
        };
    }
};

handlers.RejoinMatch = function (args) {
    
    var playerId = currentPlayerId;
    var state    = loadMatchState(args.matchId);
    if (!state) return { error: "Match nao encontrado" };

    if (state.IsActive) {
        var ps = getPlayerState(state, playerId);
        if (ps) { ps.IsConnected = true; ps.ConsecutiveMissedRounds = 0; }
        saveMatchState(state);
    }

    return {
        status:           "ok",
        fullState:        state,
        currentQuestion:  state.Phase === "Question" ? loadQuestion(state, state.CurrentRoundState.QuestionId) : null,
        serverTimestampMs: Date.now()
    };
};

handlers.AbandonMatch = function (args) {
    
    var loserId = currentPlayerId;
    var state   = loadMatchState(args.matchId);
    if (!state)             return { error: "Match nao encontrado" };
    if (!state.IsActive)    return { status: "already_ended", winnerId: state.WinnerId };

    var winnerId = (state.Player1Id === loserId) ? state.Player2Id : state.Player1Id;
    if (!winnerId) {
        // Fallback robusto se o ID oposto estiver nulo no banco
        winnerId = (state.Player1Id && state.Player1Id !== loserId) ? state.Player1Id : state.Player2Id;
    }

    finalizeMatch(state, winnerId, "Abandonment");
    return { status: "ok", winnerId: winnerId, loserId: loserId };
};

handlers.FinalizeMatch = function (args) {
    
    var state = loadMatchState(args.matchId);
    if (!state)          return { error: "Match nao encontrado" };
    if (!state.IsActive) return { status: "already_ended" };   // evita double-processing
    finalizeMatch(state, args.winnerId, "Abandonment");
    return { status: "ok" };
};

// --- Lógica interna de rodada ---

function processRoundInternal(originalState) {
    // Recarrega estado fresco antes de computar resultados.
    // O chamador (ProcessRound) pode ter lido um estado levemente
    // desatualizado — respostas salvas por SubmitAnswer milissegundos antes poderiam
    // nao estar visiveis, fazendo o jogador ser computado como "nao respondeu".
    var state = loadMatchState(originalState.MatchId);
    if (!state || state.CurrentRound !== originalState.CurrentRoundState.RoundNumber) {
        state = originalState; // fallback: rodada mudou ou leitura falhou
    }

    var round = state.CurrentRoundState;
    if (round.IsProcessed) return buildRoundResponse(state, true); // idempotente

    // Bug 1 fix: protecao contra eventual consistency no HP.
    // Se o estado relido tiver LastProcessedRoundResult da rodada imediatamente anterior,
    // usa os HPAfter salvos como base — evita que uma replica desatualizada do
    // TitleInternalData faca o calculo partir de um HP incorreto (ex: 100 em vez de 92),
    // o que causava o efeito de "vida voltando" visto nos logs.
    var lpr = state.LastProcessedRoundResult;
    if (lpr && lpr.RoundNumber === round.RoundNumber - 1) {
        if (typeof lpr.Player1HPAfter === "number") state.Player1State.HP = lpr.Player1HPAfter;
        if (typeof lpr.Player2HPAfter === "number") state.Player2State.HP = lpr.Player2HPAfter;
    }

    var p1Act   = round.Player1Action;
    var p2Act   = round.Player2Action;
    var p1State = state.Player1State;
    var p2State = state.Player2State;

    var p1Result = computePlayerResult(p1Act, p1State, p2Act, p2State, round.CorrectAnswerId);
    var p2Result = computePlayerResult(p2Act, p2State, p1Act, p1State, round.CorrectAnswerId);

    var p1HPBefore = p1State.HP;
    var p2HPBefore = p2State.HP;

    if (!p2Result.WasShielded) p2State.HP -= p1Result.DamageDealt;
    if (!p1Result.WasShielded) p1State.HP -= p2Result.DamageDealt;

    if (p1Act.ActivatedPowerUp === "Steal") { p1State.HP += DAMAGE_CONFIG.StealAmount; p2State.HP -= DAMAGE_CONFIG.StealAmount; }
    if (p2Act.ActivatedPowerUp === "Steal") { p2State.HP += DAMAGE_CONFIG.StealAmount; p1State.HP -= DAMAGE_CONFIG.StealAmount; }

    if (!p1Act.HasAnswered) p1State.HP -= DAMAGE_CONFIG.SelfDamageNoAnswer;
    if (!p2Act.HasAnswered) p2State.HP -= DAMAGE_CONFIG.SelfDamageNoAnswer;

    p1State.HP = Math.max(0, p1State.HP);
    p2State.HP = Math.max(0, p2State.HP);

    p1Result.HPBefore = p1HPBefore; p1Result.HPAfter = p1State.HP;
    p2Result.HPBefore = p2HPBefore; p2Result.HPAfter = p2State.HP;

    p1State.Streak = p1Result.Result === 1 ? p1State.Streak + 1 : 0;
    p2State.Streak = p2Result.Result === 1 ? p2State.Streak + 1 : 0;
    p1Result.StreakAfter = p1State.Streak;
    p2Result.StreakAfter = p2State.Streak;

    if (!p1Act.HasAnswered) p1State.ConsecutiveMissedRounds++; else p1State.ConsecutiveMissedRounds = 0;
    if (!p2Act.HasAnswered) p2State.ConsecutiveMissedRounds++; else p2State.ConsecutiveMissedRounds = 0;

    updatePowerUpCharges(p1State, p1Act);
    updatePowerUpCharges(p2State, p2Act);

    round.Player1Result = p1Result;
    round.Player2Result = p2Result;
    round.IsProcessed   = true;
    state.LastProcessedRound = round.RoundNumber;

    // Bug 1 fix: persiste o HP final desta rodada como ancora para a proxima.
    // Quando processRoundInternal for chamado na rodada N+1 e fizer um reload fresco,
    // pode receber uma replica ainda desatualizada com HP errado. Este campo garante
    // que a base do HP seja sempre o valor correto da ultima rodada processada.
    state.LastProcessedRoundResult = {
        RoundNumber:    round.RoundNumber,
        Player1HPAfter: p1State.HP,
        Player2HPAfter: p2State.HP
    };

    var afkP1     = p1State.ConsecutiveMissedRounds >= MATCH_CONFIG.AfkRoundLimit;
    var afkP2     = p2State.ConsecutiveMissedRounds >= MATCH_CONFIG.AfkRoundLimit;
    var hpOver    = p1State.HP <= 0 || p2State.HP <= 0;
    var roundsMax = round.RoundNumber >= MATCH_CONFIG.MaxRounds;
    var matchOver = hpOver || roundsMax || afkP1 || afkP2;
    var winnerId  = null;
    var endReason = 0;

    // Valores numericos espelham o enum C# MatchEndReason: HPDepleted=0, RoundsOver=1, Abandonment=2
    if (matchOver) {
        if      (afkP1 && afkP2) { winnerId = null;            endReason = 2; }
        else if (afkP1)          { winnerId = state.Player2Id; endReason = 2; }
        else if (afkP2)          { winnerId = state.Player1Id; endReason = 2; }
        else if (roundsMax)      { winnerId = determineWinnerByHP(state); endReason = 1; }
        else                     { winnerId = determineWinnerByHP(state); endReason = 0; }
        state.IsActive  = false;
        state.WinnerId  = winnerId;
        state.EndReason = endReason;
        state.Phase     = "MatchEnd";
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
        Breakdown:   { BaseDamage: 0, SpeedBonus: 0, StreakBonus: 0, PowerUpBonus: 0, StolenHP: 0, SelfDamage: 0 }
    };

    // Valores numéricos espelham o enum C# AnswerResult: NotAnswered=0, Correct=1, Incorrect=2
    if (!attackerAct.HasAnswered) { result.Result = 0; return result; }

    var correct   = (attackerAct.AnswerId === correctId);
    result.Result = correct ? 1 : 2;
    if (!correct) return result;

    var streak = attackerState.Streak + 1;
    result.Breakdown.BaseDamage  = DAMAGE_CONFIG.BaseDamage;
    result.Breakdown.StreakBonus = getStreakBonus(streak);

    if (defenderAct.HasAnswered && defenderAct.AnswerId === correctId) {
        var diff = Math.abs(attackerAct.AnswerTimestampMs - defenderAct.AnswerTimestampMs);
        if (diff > MATCH_CONFIG.SpeedBonusThresholdMs && attackerAct.AnswerTimestampMs < defenderAct.AnswerTimestampMs)
            result.Breakdown.SpeedBonus = DAMAGE_CONFIG.SpeedBonus;
    }

    if (attackerAct.ActivatedPowerUp === "Bet") result.Breakdown.PowerUpBonus = DAMAGE_CONFIG.BetBonus;

    if (isShielded(defenderState, defenderAct)) {
        result.WasShielded = true;
        result.Breakdown.BaseDamage = result.Breakdown.SpeedBonus = result.Breakdown.StreakBonus = result.Breakdown.PowerUpBonus = 0;
    }

    var bd = result.Breakdown;
    result.DamageDealt = bd.BaseDamage + bd.SpeedBonus + bd.StreakBonus + bd.PowerUpBonus;
    return result;
}

// --- Helpers de partida ---

function isShielded(state, action) {
    return action.ActivatedPowerUp === "SimpleShield"
        || action.ActivatedPowerUp === "DoubleShield"
        || state.DoubleShieldCharges > 0;
}

function updatePowerUpCharges(state, action) {
    if (isShielded(state, action) && state.DoubleShieldCharges > 0) state.DoubleShieldCharges--;
    if (action.ActivatedPowerUp === "DoubleShield")   { state.DoubleShieldCharges = 1; state.HasUsedPowerUp = true; }
    else if (action.ActivatedPowerUp !== "None")       { state.HasUsedPowerUp = true; }
}

function bothPlayersAnswered(state) {
    return state.CurrentRoundState.Player1Action.HasAnswered
        && state.CurrentRoundState.Player2Action.HasAnswered;
}

function getPlayerAction(state, playerId) {
    return playerId === state.Player1Id ? state.CurrentRoundState.Player1Action : state.CurrentRoundState.Player2Action;
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
    var round = state.CurrentRoundState || {};
    var p1Res = round.Player1Result || { Result: 0, DamageDealt: 0, Breakdown: { BaseDamage: 0, SpeedBonus: 0, StreakBonus: 0, PowerUpBonus: 0, StolenHP: 0, SelfDamage: 0 } };
    var p2Res = round.Player2Result || { Result: 0, DamageDealt: 0, Breakdown: { BaseDamage: 0, SpeedBonus: 0, StreakBonus: 0, PowerUpBonus: 0, StolenHP: 0, SelfDamage: 0 } };

    // Fallback de seguranca para evitar que o C# parseie o HP como 0 caso o resultado venha vazio
    p1Res.HPAfter = state.Player1State.HP;
    p1Res.StreakAfter = state.Player1State.Streak;
    p2Res.HPAfter = state.Player2State.HP;
    p2Res.StreakAfter = state.Player2State.Streak;

    return {
        RoundNumber:      round.RoundNumber || state.CurrentRound,
        AlreadyProcessed: alreadyProcessed,
        CorrectAnswerId:  round.CorrectAnswerId || "A", // Fallback seguro caso abandone antes da questao ser gerada
        Player1Result:    p1Res,
        Player2Result:    p2Res,
        Player1HP:        state.Player1State.HP,
        Player2HP:        state.Player2State.HP,
        IsMatchOver:      !state.IsActive,
        WinnerId:         state.WinnerId,
        EndReason:        state.EndReason || 0
    };
}

// Pool de perguntas para cache local no cliente.
// Omite CorrectOptionId para não vazar gabarito — servidor continua validando.
function buildClientPool(serverPool) {
    if (!serverPool || !Array.isArray(serverPool)) return [];
    var out = [];
    for (var i = 0; i < serverPool.length; i++) {
        var q = serverPool[i];
        if (!q) continue;
        out.push({ QuestionId: q.QuestionId, Text: q.Text, ThemeName: q.ThemeName, Options: q.Options });
    }
    return out;
}

// reason aceita string ou número; normaliza para número antes de salvar
function finalizeMatch(state, winnerId, reason) {
    var reasonMap = { "HPDepleted": 0, "RoundsOver": 1, "Abandonment": 2, "Disconnected": 3 };
    var reasonNum = (typeof reason === "number") ? reason : (reasonMap[reason] !== undefined ? reasonMap[reason] : 0);
    state.IsActive  = false;
    state.WinnerId  = winnerId;
    state.EndReason = reasonNum;
    state.Phase     = "MatchEnd";
    saveMatchState(state);
    removeFromActiveIndex(state.MatchId);
    updatePlayerStats(state, winnerId);
    return { status: "finalized", winnerId: winnerId };
}

function getPlayerDisplayInfo(playerId) {
    try {
        var r = server.GetUserData({ PlayFabId: playerId, Keys: ["player_profile"] });
        if (!r.Data || !r.Data["player_profile"]) return { displayName: "", level: 1, avatarId: "" };
        var p = JSON.parse(r.Data["player_profile"].Value);
        return { displayName: p.displayName || "", level: p.level || 1, avatarId: p.avatarId || "" };
    } catch(e) {
        return { displayName: "", level: 1, avatarId: "" };
    }
}

function makePlayerState(playerId, displayName, level, equippedPowerUp, avatarId) {
    return {
        PlayerId:                playerId,
        DisplayName:             displayName || "",
        Level:                   level || 1,
        HP:                      MATCH_CONFIG.InitialHP,
        Streak:                  0,
        HasUsedPowerUp:          false,
        IsReady:                 false,
        EquippedPowerUp:         equippedPowerUp || "None",
        DoubleShieldCharges:     0,
        IsConnected:             true,
        ConsecutiveMissedRounds: 0,
        AvatarId:                avatarId || "skinDefault"
    };
}

function makeAction(playerId) {
    return { PlayerId: playerId, AnswerId: null, AnswerTimestampMs: 0, HasAnswered: false, ActivatedPowerUp: "None" };
}

// --- Carregamento de decks e perguntas ---

function getEquippedPowerUp(playerId) {
    if (!isNonEmptyString(playerId)) return "None";
    // Lê do player_profile (UserData) — mesma fonte que o cliente C# usa para exibir o power-up equipado.
    // UserInternalData["EquippedPowerUp"] nunca é populado pela app atual.
    try {
        var r = server.GetUserData({ PlayFabId: playerId, Keys: ["player_profile"] });
        if (r.Data && r.Data["player_profile"]) {
            var p = JSON.parse(r.Data["player_profile"].Value);
            if (p && isNonEmptyString(p.equippedPowerUp) && p.equippedPowerUp !== "None")
                return p.equippedPowerUp;
        }
    } catch (e) { }
    return "None";
}

// Converte DeckSchemaV2 em array de perguntas prontas para o pool
function parseDeckQuestions(deckData, themeName) {
    if (!deckData || !deckData.questions) return [];
    var questions = [];
    for (var i = 0; i < deckData.questions.length; i++) {
        var q = deckData.questions[i];
        if (!q || !q.options || q.options.length < 2) continue;
        var options   = [];
        var correctId = null;
        for (var j = 0; j < q.options.length; j++) {
            var optId = String.fromCharCode(65 + j); // A, B, C, D
            options.push({ Id: optId, Text: q.options[j].text || "" });
            if (q.options[j].is_correct && !correctId) correctId = optId;
        }
        if (!correctId) continue;
        questions.push({
            QuestionId:      q.id || (themeName + "_" + i),
            Text:            q.text || "",
            ThemeId:         themeName,
            ThemeName:       themeName,
            Options:         options,
            CorrectOptionId: correctId
        });
    }
    return questions;
}

// Lê deck_id do CustomData de cada item de deck no inventário do jogador.
// O deck_id é a chave no TitleData onde estão as perguntas do deck.
function getPlayerDeckEntries(playerId) {
    var entries = [];
    var seenKeys = {};
    if (!isNonEmptyString(playerId)) return entries;
    try {
        var inv = server.GetUserInventory({ PlayFabId: playerId });
        if (!inv || !inv.Inventory) return entries;

        // deck_id vive no CustomData do CATÁLOGO (string JSON), não na instância do inventário
        var cat = server.GetCatalogItems({ CatalogVersion: DEFAULT_CATALOG_VERSION });
        var deckIdByItem = {};
        if (cat && cat.Catalog) {
            for (var c = 0; c < cat.Catalog.length; c++) {
                var ci = cat.Catalog[c];
                if (!ci || !isNonEmptyString(ci.CustomData)) continue;
                var parsed = safeJsonParse(ci.CustomData);
                if (parsed.success && parsed.value && parsed.value.deck_id)
                    deckIdByItem[ci.ItemId] = String(parsed.value.deck_id).trim();
            }
        }

        for (var i = 0; i < inv.Inventory.length; i++) {
            var item = inv.Inventory[i];
            if (!item || !isNonEmptyString(item.ItemId)) continue;
            var deckId = deckIdByItem[item.ItemId];
            if (deckId && !seenKeys[deckId]) {
                seenKeys[deckId] = true;
                entries.push({ key: deckId });
            }
        }
    } catch (e) { }
    return entries;
}

// Monta o pool de 20 perguntas combinando os decks de ambos os jogadores.
// Fluxo:
//   1. Lê CustomData.deck_id do inventário de cada jogador → TitleData keys
//   2. Une as keys (sem duplicatas)
//   3. Carrega todos os decks em uma única chamada GetTitleData
//   4. Fisher-Yates shuffle → 20 perguntas
function buildQuestionPool(p1Id, p2Id) {
    // 1. Entries { key } de cada jogador via inventário
    var p1Entries = getPlayerDeckEntries(p1Id);
    var p2Entries = getPlayerDeckEntries(p2Id);

    // 2. União sem duplicatas de keys
    var entryMap = {};
    var allEntries = [];
    function addEntry(e) {
        if (e && e.key && !entryMap[e.key]) { entryMap[e.key] = e; allEntries.push(e); }
    }
    for (var i = 0; i < p1Entries.length; i++) addEntry(p1Entries[i]);
    for (var i = 0; i < p2Entries.length; i++) addEntry(p2Entries[i]);

    var keysToLoad = allEntries.map(function(e) { return e.key; });

    // 3. Carrega decks do TitleData
    var allQuestions = [];
    var seenIds      = {};

    if (keysToLoad.length > 0) {
        try {
            var deckResult = server.GetTitleData({ Keys: keysToLoad });
            for (var k = 0; k < allEntries.length; k++) {
                var key = allEntries[k].key;
                if (!deckResult.Data || !deckResult.Data[key]) continue;
                try {
                    var deckData  = JSON.parse(deckResult.Data[key]);
                    var themeName = deckData.theme || key;
                    var questions = parseDeckQuestions(deckData, themeName);
                    for (var q = 0; q < questions.length; q++) {
                        if (!seenIds[questions[q].QuestionId]) {
                            seenIds[questions[q].QuestionId] = true;
                            allQuestions.push(questions[q]);
                        }
                    }
                } catch (e) { /* deck corrompido: ignora */ }
            }
        } catch (e) { /* GetTitleData falhou: cai no fallback */ }
    }

    // Fallback estático se nenhuma pergunta foi carregada
    if (allQuestions.length === 0) {
        for (var i = 0; i < MATCH_CONFIG.MaxRounds; i++) {
            allQuestions.push({
                QuestionId:      "fallback_" + (i + 1),
                Text:            "Qual é a capital do Brasil?",
                ThemeId:         "Historia", ThemeName: "Historia",
                Options:         [{ Id: "A", Text: "Brasília" }, { Id: "B", Text: "São Paulo" }, { Id: "C", Text: "Rio de Janeiro" }, { Id: "D", Text: "Salvador" }],
                CorrectOptionId: "A"
            });
        }
        return allQuestions;
    }

    // 4. Fisher-Yates shuffle
    for (var i = allQuestions.length - 1; i > 0; i--) {
        var j   = Math.floor(Math.random() * (i + 1));
        var tmp = allQuestions[i]; allQuestions[i] = allQuestions[j]; allQuestions[j] = tmp;
    }

    // Garante exatamente MaxRounds perguntas (cicla se necessário)
    var pool = [];
    for (var i = 0; i < MATCH_CONFIG.MaxRounds; i++)
        pool.push(allQuestions[i % allQuestions.length]);
    return pool;
}

// Seleciona 2 índices de respostas erradas para o EliminateTwo (nunca elimina a correta)
function calcularIndicesEliminados(options, correctOptionId) {
    // Sem gabarito definido não é possível garantir que a correta não seja eliminada
    if (!options || !correctOptionId) return [];
    var errados = [];
    for (var i = 0; i < options.length; i++) {
        if (options[i] && options[i].Id !== correctOptionId) errados.push(i);
    }
    // Embaralha e pega os 2 primeiros
    for (var i = errados.length - 1; i > 0; i--) {
        var j = Math.floor(Math.random() * (i + 1));
        var tmp = errados[i]; errados[i] = errados[j]; errados[j] = tmp;
    }
    return errados.slice(0, 2);
}

// Retorna a pergunta completa para a rodada (pool armazena objetos, não IDs)
function getQuestionForRound(state, roundNumber) {
    var idx = roundNumber - 1;
    if (idx < 0 || idx >= state.QuestionPool.length) return null;
    return state.QuestionPool[idx];
}

// Busca pergunta pelo ID dentro do pool da partida
function loadQuestion(state, questionId) {
    if (!state || !state.QuestionPool) return null;
    for (var i = 0; i < state.QuestionPool.length; i++) {
        if (state.QuestionPool[i].QuestionId === questionId) return state.QuestionPool[i];
    }
    return null;
}

// Atualiza estatísticas e moedas de ambos os jogadores no fim da partida.
// Requer que as estatísticas no PlayFab estejam configuradas com agregação "Sum".
function updatePlayerStats(state, winnerId) {
    var isDraw         = !winnerId;
    var isAbandonment  = state.EndReason === 2; // MatchEndReason.Abandonment

    aplicarResultadoJogador(state.Player1Id, state.Player1Id === winnerId, isDraw, isAbandonment);
    aplicarResultadoJogador(state.Player2Id, state.Player2Id === winnerId, isDraw, isAbandonment);
}

function aplicarResultadoJogador(playerId, ganhou, empate, abandono) {
    // XP: vitória=100, empate=50, abandono(vítima)=-10, derrota=20
    var xp     = ganhou ? 100 : (empate ? 50 : (abandono ? -10 : 20));
    // Moedas: vitória=80, empate=20, abandono=0, derrota=10
    var moedas = ganhou ? 80  : (empate ? 20 : (abandono ? 0   : 10));

    // Estatísticas (+1 por partida, acumula com agregação Sum no PlayFab)
    var stats = [
        { StatisticName: "partidas_totais_semanal", Value: 1 },
        { StatisticName: "partidas_totais_mensal",  Value: 1 },
        { StatisticName: "partidas_totais",         Value: 1 }
    ];

    if (xp > 0) {
        stats.push({ StatisticName: "xp_semanal", Value: xp });
        stats.push({ StatisticName: "xp_mensal",  Value: xp });
        stats.push({ StatisticName: "xp_total",   Value: xp });
    }

    if (ganhou || empate) {
        stats.push({ StatisticName: "vitorias_semanal", Value: 1 });
        stats.push({ StatisticName: "vitorias_mensal",  Value: 1 });
        stats.push({ StatisticName: "wins",             Value: 1 });
    } else {
        stats.push({ StatisticName: "losses", Value: 1 });
    }

    try {
        server.UpdatePlayerStatistics({ PlayFabId: playerId, Statistics: stats });
    } catch (e) { log.info("updatePlayerStats falhou para " + playerId + ": " + JSON.stringify(e)); }

    // Credita moedas via moeda virtual "BC" (BrainCoins)
    if (moedas > 0) {
        try {
            server.AddUserVirtualCurrency({ PlayFabId: playerId, VirtualCurrency: "BC", Amount: moedas });
        } catch (e) { log.info("AddUserVirtualCurrency falhou para " + playerId + ": " + JSON.stringify(e)); }
    }
}

// ============================================================
// PERFIL DO JOGADOR
// Atualiza o avatar equipado no payload player_profile.
// Valida posse da skin no inventário antes de persistir.
// ============================================================

handlers.EquipAvatarSkin = function(args) {
    var avatarId = args && args.avatarId ? String(args.avatarId) : "";

    if (!avatarId)
        throw new Error("avatarId inválido");

    if (avatarId.toLowerCase().indexOf("skin") !== 0)
        throw new Error("avatarId inválido para skin");

    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var items = inventory && inventory.Inventory ? inventory.Inventory : [];
    var ownsSkin = false;

    for (var i = 0; i < items.length; i++) {
        var item = items[i];
        if (item && item.ItemId && String(item.ItemId).toLowerCase() === avatarId.toLowerCase()) {
            ownsSkin = true;
            break;
        }
    }

    if (!ownsSkin)
        throw new Error("Skin não encontrada no inventário do jogador");

    var userData = server.GetUserData({
        PlayFabId: currentPlayerId,
        Keys: ["player_profile"]
    });

    var profileJson = userData && userData.Data && userData.Data.player_profile ? userData.Data.player_profile.Value : null;
    var profile = {};

    if (profileJson) {
        try {
            profile = JSON.parse(profileJson) || {};
        } catch (e) {
            profile = {};
        }
    }

    profile.avatarId = avatarId;

    server.UpdateUserData({
        PlayFabId: currentPlayerId,
        Data: {
            player_profile: JSON.stringify(profile)
        }
    });

    return {
        success: true,
        avatarId: avatarId
    };
};

// ============================================================
// SEÇÃO 4 — LOJA
// ============================================================

handlers.PurchaseItemSecure = function (args, context) {
    var itemId          = args && args.itemId          ? String(args.itemId)          : null;
    var virtualCurrency = args && args.virtualCurrency ? String(args.virtualCurrency) : null;
    var price           = args && args.price           ? parseInt(args.price, 10)     : 0;
    var storeId         = args && args.storeId         ? String(args.storeId)         : "store_default";

    if (!itemId)          return { success: false, error: "itemId is required" };
    if (!virtualCurrency) return { success: false, error: "virtualCurrency is required" };
    if (!price || price <= 0) return { success: false, error: "price must be > 0" };

    var inventory      = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var currentBalance = inventory.VirtualCurrency && inventory.VirtualCurrency[virtualCurrency]
        ? inventory.VirtualCurrency[virtualCurrency] : 0;

    if (currentBalance < price) {
        return { success: false, error: "insufficient_balance", itemId: itemId, currencyCode: virtualCurrency, price: price, currentBalance: currentBalance };
    }

    var catalogItems = server.GetCatalogItems({ CatalogVersion: "mainCatalog" });
    var itemFound    = false;
    if (catalogItems && catalogItems.Catalog) {
        for (var i = 0; i < catalogItems.Catalog.length; i++) {
            if (catalogItems.Catalog[i].ItemId === itemId) { itemFound = true; break; }
        }
    }
    if (!itemFound) return { success: false, error: "item_not_found", itemId: itemId };

    var subtractResult = server.SubtractUserVirtualCurrency({
        PlayFabId: currentPlayerId,
        VirtualCurrency: virtualCurrency,
        Amount: price
    });

    var newBalance = subtractResult.Balance !== undefined ? subtractResult.Balance : (currentBalance - price);
    return { success: true, operation: "purchase", itemId: itemId, currencyCode: virtualCurrency, priceDeducted: price, newBalance: newBalance, storeId: storeId };
};

handlers.DebugPlayerInventory = function (args, context) {
    var inventory = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var items = [];

    if (inventory && inventory.Inventory) {
        for (var i = 0; i < inventory.Inventory.length; i++) {
            var item = inventory.Inventory[i];
            items.push({
                ItemId:     item.ItemId,
                CustomData: item.CustomData || null
            });
        }
    }

    return {
        totalItems: items.length,
        items: items
    };
};

//
// Equipar skin
//
handlers.EquiparSkinSimples = function (args, context) {
    var skinId = (args && args.skinId) ? String(args.skinId) : "skinDefault";

    // Salva essa informação no banco de dados do PlayFab do jogador
    server.UpdateUserReadOnlyData({
        PlayFabId: currentPlayerId,
        Data: { "EquippedSkin": skinId }
    });

    // Retorna para o Unity dizendo que deu tudo certo
    return { success: true, skinSalva: skinId };
};

handlers.DebugActiveMatches = function (args, context) {
    return {
        activeMatchIds:   loadActiveIndex(),
        askedFor:         args ? args.matchId : null,
        existsForAskedId: args && args.matchId ? !!loadMatchState(args.matchId) : null
    };
};