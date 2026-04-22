/*
 * CloudScript - Deck Admin CRUD (TitleData)
 *
 * Handlers:
 * - ValidatePlayerRole
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

handlers.DeckAdminListCatalog = function (args, context) {
    var guard = requireAdmin();
    if (!guard.success) {
        return guard;
    }

    var indexResult = loadDeckIndex();
    if (!indexResult.success) {
        return indexResult;
    }

    return {
        success: true,
        operation: "list",
        deckIndex: indexResult.deckIndex
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

    if (key.indexOf("cartas_") !== 0) {
        return fail("key must start with 'cartas_'");
    }

    var regex = /^cartas_[a-z0-9_]+$/;
    if (!regex.test(key)) {
        return fail("key must use only lowercase letters, numbers and underscore");
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
