# Deck Admin CRUD - Implementation Plan

## 1) Current architecture snapshot

### Login and authorization flow (already implemented)
- Scene Login uses LoginScreenHandler + PlayFabLogin.
- LoginScreenHandler performs login by email/password through PlayFabService.
- On login success, LoginScreenHandler calls AuthorizationService.ValidatePlayerRole.
- AuthorizationService invokes CloudScript function ValidatePlayerRole.
- Role result is mapped to UserRole (User or Admin), then scene is resolved.

### Deck data flow (already implemented)
- DeckService loads deck_index from TitleData key deck_index.
- Each category references a deck key (example: cartas_matematica).
- DeckService loads each deck JSON and deserializes as DeckWrapper { deck: [...] }.
- Runtime gameplay consumes cards from DeckService cache.

### Current gaps
- No admin scene dedicated to deck operations.
- No active upload/validation pipeline (legacy DeckUploader/DeckValidator were disabled).
- No server-side CRUD handlers for TitleData decks.
- No UI guardrail for admin-only editor operations.

## 2) Target architecture for admin CRUD

### Scene topology
- Login (existing): authentication and role validation.
- AdminDeckCrud (new): admin-only panel for deck CRUD operations.
- LoginSuccess (existing): optional generic post-login scene for non-admin users.

### Scene routing rules
- User role -> LoginSuccess (or current user scene).
- Admin role -> AdminDeckCrud.
- If role validation fails -> stay in Login and display error.

### Required Unity scripts (new)
- AdminDeckCrudController:
  - Boots required services.
  - Calls cloudscript handlers for list/get/create/update/delete.
  - Coordinates UI state (loading, success, error).
- AdminDeckCloudScriptService:
  - Thin wrapper around ExecuteCloudScript with typed request/response models.
  - Centralizes function names.
- DeckPayloadEditorSerializer:
  - Converts textarea JSON to DeckWrapper and back.
  - Local pre-validation before server call.
- AdminAccessGuard:
  - On scene start, checks AuthorizationService.CurrentRole.
  - Redirects to Login if not admin.

### Recommended folder structure
- Assets/Scripts/Admin/
  - AdminDeckCrudController.cs
  - AdminDeckCloudScriptService.cs
  - AdminAccessGuard.cs
  - Models/
    - AdminDeckDtos.cs

## 3) AdminDeckCrud scene design

## Layout blocks
- Header:
  - Logged user identity (email/customId).
  - Current role badge.
  - Refresh button.
  - Logout/Back button.
- Catalog panel (left):
  - List of categories from deck_index.
  - Search by nome/key.
  - Create category button.
  - Select item to load deck in editor.
- Deck editor panel (right):
  - nome input.
  - key input (prefix cartas_).
  - Large JSON editor for deck payload.
  - Actions: Validate, Save(Create/Update), Delete.
  - Validation output panel with errors/warnings.
- Footer status:
  - Last operation, timestamp, and server result.

## UX flows
1. Open scene -> run DeckAdminListCatalog -> fill list.
2. Select category -> run DeckAdminGetDeck -> populate editor.
3. Create:
   - Fill nome + key + payload.
   - Run validate endpoint.
   - If valid, run create endpoint.
4. Update:
   - Edit name and/or payload.
   - Validate then update endpoint.
5. Delete:
   - Confirm modal.
   - Delete endpoint.
   - Optional "clear deck content" flag.

## 4) Data contracts

### deck_index (TitleData)
{
  "versao": 1,
  "categorias": [
    { "nome": "Matematica", "key": "cartas_matematica" }
  ]
}

### deck payload (TitleData by key)
{
  "deck_id": "deck_matematica_01",
  "theme": "Matematica",
  "questions": [
    {
      "id": "mat_001",
      "text": "Qual e 2+2?",
      "options": [
        { "text": "1", "is_correct": false },
        { "text": "2", "is_correct": false },
        { "text": "3", "is_correct": false },
        { "text": "4", "is_correct": true }
      ],
      "time_limit": 20
    }
  ]
}

## 5) Validation rules (server-side)

### Category/index validation
- nome is required and unique in deck_index.
- key is required and unique in deck_index.
- key format must match: cartas_[a-z0-9_]+.
- versao is incremented on each create/update/delete affecting index.

### Card/deck validation
- payload must be object containing deck_id, theme and questions array.
- questions must contain at least one entry.
- each question must contain id, text, options, time_limit.
- question id must be unique inside deck.
- options must have at least 4 entries.
- each option must contain text and is_correct.
- each question must have exactly one option with is_correct=true.
- time_limit must be integer >= 1.
- theme mismatch with categoria nome generates warning.

### Relationship validation requested
- A question must always have exactly one correct answer.
- A question must have at least 4 total answers.

## 6) CloudScript handlers (implemented in this plan package)

- ValidatePlayerRole
- DeckAdminListCatalog
- DeckAdminGetDeck
- DeckAdminCreateDeck
- DeckAdminUpdateDeck
- DeckAdminDeleteDeck
- DeckAdminValidateDeckPayload

All handlers are implemented in Docs/PlayFab/CloudScriptDeckAdminCrud.js.

## 7) Security and governance

- Every deck CRUD handler requires admin role from UserInternalData role=admin.
- Client must never write TitleData directly.
- Non-admin calls return forbidden error.
- Keep GeneratePlayStreamEvent true in production calls for auditing.
- Consider migrating to Azure Functions + secret-backed admin APIs if stronger controls are needed later.

## 8) Rollout plan

### Phase 1 - Backend ready
- Publish CloudScriptDeckAdminCrud.js in PlayFab Automation/CloudScript.
- Set role=admin in UserInternalData for admin users.
- Smoke test handlers with PlayFab Game Manager cloudscript console.

### Phase 2 - Unity integration
- Create AdminDeckCrud scene and UI.
- Implement AdminDeckCloudScriptService and controller.
- Route adminSuccessScene in LoginScreenHandler to AdminDeckCrud.

### Phase 3 - Hardening
- Add retry and user-friendly errors.
- Add edit lock policy in process (only one admin editing same deck at a time).
- Add JSON import/export helpers for bulk editing.

### Phase 4 - QA
- Positive tests for create/update/delete/list/get.
- Negative tests for invalid payloads and non-admin access.
- Regression test DeckService runtime after CRUD updates.

## 9) Suggested acceptance criteria

- Admin can create new category + deck and it appears in deck_index.
- Admin can edit category name and deck questions.
- Admin can delete category and it disappears from deck_index.
- Invalid deck payloads are blocked with explicit validation errors.
- Non-admin users cannot execute CRUD handlers.
- Runtime DeckService can still read newly created decks without code changes.
