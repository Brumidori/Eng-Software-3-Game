# CloudScript Deck Admin CRUD - Usage

## File
- CloudScript source: Docs/PlayFab/CloudScriptDeckAdminCrud.js

## Prerequisites
- User must be authenticated.
- UserInternalData key role must be admin for admin users.
- deck_index must exist in TitleData.

## Handlers

### ValidatePlayerRole
Input:
{}

Output:
{
  "success": true,
  "role": "admin"
}

### DeckAdminListCatalog
Input:
{}

Output:
{
  "success": true,
  "operation": "list",
  "deckIndex": {
    "versao": 2,
    "categorias": [
      { "nome": "Matematica", "key": "cartas_matematica" }
    ]
  }
}

### DeckAdminGetDeck
Input:
{
  "key": "cartas_matematica"
}

Output (resumo):
{
  "success": true,
  "operation": "get",
  "key": "cartas_matematica",
  "nome": "Matematica",
  "deck": { "...": "schema completo" },
  "deckJson": "{...json string...}",
  "validationWarnings": []
}

### DeckAdminCreateDeck
Input:
{
  "nome": "Ciencia",
  "key": "cartas_ciencia",
  "deck": {
    "deck_id": "deck_ciencia_01",
    "theme": "Ciencia",
    "questions": [
      {
        "id": "cie_001",
        "text": "Qual e o simbolo do ouro?",
        "options": [
          { "text": "Au", "is_correct": true },
          { "text": "Ag", "is_correct": false },
          { "text": "Fe", "is_correct": false },
          { "text": "Cu", "is_correct": false }
        ],
        "time_limit": 20
      }
    ]
  }
}

### DeckAdminUpdateDeck
Input (rename + content):
{
  "key": "cartas_ciencia",
  "nome": "Ciencias",
  "deck": {
    "deck_id": "deck_ciencia_01",
    "theme": "Ciencias",
    "questions": [
      {
        "id": "cie_001",
        "text": "Qual e o simbolo quimico do ouro?",
        "options": [
          { "text": "Au", "is_correct": true },
          { "text": "Ag", "is_correct": false },
          { "text": "Cu", "is_correct": false },
          { "text": "Fe", "is_correct": false }
        ],
        "time_limit": 20
      }
    ]
  }
}

### DeckAdminDeleteDeck
Input:
{
  "key": "cartas_ciencia",
  "clearDeckContent": true
}

Output (resumo):
{
  "success": true,
  "operation": "delete",
  "key": "cartas_ciencia",
  "removedCategoryName": "Ciencia",
  "versao": 3,
  "contentCleared": true,
  "keyDeleted": true,
  "keyDeleteMode": "DeleteTitleData"
}

### DeckAdminValidateDeckPayload
Input:
{
  "nome": "Ciencia",
  "key": "cartas_ciencia",
  "deck": {
    "deck_id": "deck_ciencia_01",
    "theme": "Ciencia",
    "questions": [
      {
        "id": "cie_001",
        "text": "Pergunta exemplo",
        "options": [
          { "text": "A", "is_correct": false },
          { "text": "B", "is_correct": true },
          { "text": "C", "is_correct": false },
          { "text": "D", "is_correct": false }
        ],
        "time_limit": 15
      }
    ]
  }
}

Output:
{
  "success": true,
  "errors": [],
  "warnings": []
}

## Validation summary
- questions length >= 1.
- each question.options length >= 4.
- each question must have exactly one option with is_correct=true.
- question.id must be unique inside the same deck.
- question.time_limit must be integer >= 1.
- key must start with cartas_.
- only admin can execute CRUD handlers.

## Notes
- Delete removes category from deck_index.
- clearDeckContent defaults to true when omitted.
- If clearDeckContent=true, handler attempts hard delete of the deck key in TitleData using DeleteTitleData.
- If DeleteTitleData is unavailable, handler falls back to SetTitleData(null) and validates the result.
- Keep cloudscript revision strategy aligned with client (Latest vs Live).
