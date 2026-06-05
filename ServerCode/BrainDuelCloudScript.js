// ============================================================
// BrainDuelCloudScript.js — PlayFab CloudScript V1
//
// COMO USAR:
//   1. Acesse: https://developer.playfab.com
//   2. Seu título → Build → CloudScript
//   3. Cole TODO este arquivo na aba de edição
//   4. Clique em "Save" e depois "Deploy to players"
//
// Este código roda nos servidores do próprio PlayFab.
// Não é necessário Azure, servidor próprio ou conta extra.
// ============================================================

// ============================================================
// CONSTANTES
// ============================================================

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
// LOJA
// Compra server-authoritative: valida saldo, debita moeda,
// concede o item no inventario e reembolsa se a concessao falhar.
// ============================================================

handlers.PurchaseItemSecure = function (args, context) {
    var itemId          = args && args.itemId          ? String(args.itemId)          : null;
    var virtualCurrency = args && args.virtualCurrency ? String(args.virtualCurrency) : null;
    var price           = args && args.price           ? parseInt(args.price, 10)     : 0;
    var storeId         = args && args.storeId         ? String(args.storeId)         : "store_default";
    var catalogVersion  = "mainCatalog";

    if (!itemId)          return { success: false, error: "itemId is required" };
    if (!virtualCurrency) return { success: false, error: "virtualCurrency is required" };
    if (!price || price <= 0) return { success: false, error: "price must be > 0" };

    var inventory      = server.GetUserInventory({ PlayFabId: currentPlayerId });
    var currentBalance = inventory.VirtualCurrency && inventory.VirtualCurrency[virtualCurrency]
        ? inventory.VirtualCurrency[virtualCurrency] : 0;

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

    if (currentBalance < price) {
        return { success: false, error: "insufficient_balance", itemId: itemId, currencyCode: virtualCurrency, price: price, currentBalance: currentBalance };
    }

    var catalogItems = server.GetCatalogItems({ CatalogVersion: catalogVersion });
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
    var grantResult = null;

    try {
        grantResult = server.GrantItemsToUser({
            PlayFabId: currentPlayerId,
            CatalogVersion: catalogVersion,
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
