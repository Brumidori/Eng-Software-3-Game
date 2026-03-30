# 📊 Guia de Upload de Decks para PlayFab

Este guia explica como fazer upload de arquivos de perguntas (decks) para o PlayFab usando a interface visual do DeckManager.

---

## 📁 Estrutura de Arquivos

```
Assets/
├── DeckData/
│   ├── deck_index.json          ← Índice com categorias
│   ├── cartas_matematica.json   ← Perguntas de Matemática
│   ├── cartas_geografia.json    ← Perguntas de Geografia
│   └── cartas_historia.json     ← Perguntas de História
│
└── Scripts/
    ├── DeckManager.cs                   ← Interface centralizada
    ├── Services/
    │   ├── PlayFabService.cs            ← Serviço de conexão
    │   └── DeckService.cs               ← Serviço de carregamento
    ├── Tools/
    │   ├── DeckUploader.cs              ← Lógica de upload
    │   └── DeckValidator.cs             ← Lógica de validação
    └── Editor/
        └── DeckManagerEditor.cs         ← Interface Inspector
```

---

## 🔧 Como Funciona

### 1️⃣ **Estrutura do JSON - Índice (`deck_index.json`)**

```json
{
  "versao": 1,
  "categorias": [
    {
      "nome": "Matemática",
      "key": "cartas_matematica"
    },
    {
      "nome": "Geografia",
      "key": "cartas_geografia"
    }
  ]
}
```

**Explicação:**
- `versao`: Versão do índice (para controle de compatibilidade)
- `categorias`: Lista de categorias disponíveis
- `nome`: Nome da categoria exibido no app
- `key`: Nome da chave do JSON específico (sem `.json`)

---

### 2️⃣ **Estrutura do JSON - Deck (`cartas_matematica.json`)**

```json
{
  "deck": [
    {
      "id": "mat_001",
      "pergunta": "Qual é o resultado de 2 + 2?",
      "alternativas": ["3", "4", "5", "6"],
      "respostaCorreta": 1,
      "categoria": "Matemática",
      "dificuldade": "Fácil"
    }
  ]
}
```

**Explicação:**
- `id`: Identificador único da pergunta
- `pergunta`: Texto da pergunta
- `alternativas`: Array com 4 opções de resposta
- `respostaCorreta`: Índice (0-3) da resposta correta
- `categoria`: Categoria da pergunta
- `dificuldade`: Nível (Fácil, Médio, Difícil)

---

## 🚀 Como Fazer Upload (Nova Forma - Inspector)

### **Passo 1: Criar GameObject com DeckManager**

1. Crie um **GameObject vazio** na scene
2. Nomeie como `DeckManager` (opcional)

### **Passo 2: Adicionar o Script**

1. No **Inspector**, clique em **Add Component**
2. Procure por `DeckManager`
3. Selecione e adicione

### **Passo 3: Usar a Interface Visual**

No Inspector do DeckManager, você verá:

```
🚀 Inicialização
[Initialize Services]  ← Clique aqui primeiro!

📤 Upload de Decks
[Upload de Todos os Decks]
[Upload do Índice]
Selecione: ▼ Matemática
[Upload de Matemática]

🔍 Validação de Decks
[Validar Todos os Decks]
[Validar Matemática]
```

### **Fluxo Completo:**

1. **Clique** `Initialize Services` (conecta no PlayFab)
2. **Clique** `Upload de Todos os Decks` (envia JSONs)
3. **Abra o Console** (Window → General → Console)
4. **Clique** `Validar Todos os Decks` (verifica se funcionou)
5. Veja o resultado no Console! ✅

---

## ✏️ Criando Novos Decks

### 1. Crie um novo arquivo JSON em `Assets/DeckData/`

**Exemplo: `cartas_ciencia.json`**

```json
{
  "deck": [
    {
      "id": "ciencia_001",
      "pergunta": "Qual é o símbolo químico do ouro?",
      "alternativas": ["Au", "Ag", "Cu", "Fe"],
      "respostaCorreta": 0,
      "categoria": "Ciência",
      "dificuldade": "Médio"
    }
  ]
}
```

### 2. Atualize o `deck_index.json`

```json
{
  "versao": 1,
  "categorias": [
    {
      "nome": "Ciência",
      "key": "cartas_ciencia"
    }
  ]
}
```

### 3. Use o DeckManager para fazer upload

---

## 🔑 Dicas Importantes

✅ **Sempre use `respostaCorreta` como índice (0-3)**
- `respostaCorreta: 0` → Primeira alternativa está correta
- `respostaCorreta: 1` → Segunda alternativa está correta
- etc.

✅ **IDs devem ser únicos** em todo o sistema
- Convenção: `categoria_numero`
- Exemplo: `geo_001`, `geo_002`

✅ **A chave no `deck_index.json` deve corresponder ao nome do arquivo**
- Arquivo: `cartas_musica.json` → `key: "cartas_musica"`
- Arquivo: `cartas_fisica.json` → `key: "cartas_fisica"`

✅ **Manter versionamento**
- Incremente `versao` ao fazer mudanças maiores
- Ajuda a rastrear compatibilidade do cliente

✅ **Sempre com 4 alternativas**
- O sistema espera exatamente 4 opções por pergunta

✅ **Console mostra tudo**
- Abra Window → General → Console
- Veja logs de upload e validação em tempo real

---

## 📖 Referência de API (Código)

Se preferir chamar via código ao invés de botões:

```csharp
// Obter referência do DeckManager
DeckManager deckMgr = FindFirstObjectByType<DeckManager>();

// Upload
deckMgr.UploadAllDecks();
deckMgr.UploadDeckIndex();
deckMgr.UploadDeck("Matemática");

// Validação
deckMgr.ValidateAllDecks();
deckMgr.ValidateDeck("Geografia");
```

---

## 🐛 Troubleshooting

**P: "Arquivo não encontrado"**
R: Verifique se o arquivo está em `Assets/DeckData/` e tem extensão `.json`

**P: "Não está autenticado no PlayFab"**
R: Clique em `Initialize Services` no Inspector primeiro

**P: "NullReferenceException"**
R: Certifique-se de que um GameObject com DeckManager existe na Scene

**P: Erro de encoding/caracteres especiais**
R: Salve o arquivo JSON com encoding **UTF-8**

**P: Upload aparentemente funcionou, mas dados não aparecem**
R: Clique em `Validar Todos os Decks` para confirmar

---

## 📚 Estrutura Esperada no PlayFab

Seu PlayFab deve ter:

```
Title Data
├── deck_index          (JSON com índice)
├── cartas_matematica   (JSON com perguntas)
├── cartas_geografia    (JSON com perguntas)
└── cartas_historia     (JSON com perguntas)
```

Quando o app inicia:
1. `DeckManager` chama `PlayFabService.Initialize()`
2. `PlayFabService` faz login
3. `DeckService` carrega `deck_index`
4. `DeckService.LoadDeck("Matemática")` busca `cartas_matematica`
5. Perguntas são exibidas no app ✅

---

## 🎯 Próximos Passos

- [ ] Criar deck de Ciências
- [ ] Adicionar mais perguntas a categorias existentes
- [ ] Testar no app
- [ ] Fazer backup dos JSONs

---

**Último Update**: 30/03/2026
**Status**: ✅ Funcional com Interface Inspector

