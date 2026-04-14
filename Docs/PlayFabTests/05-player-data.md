# Player Data Tests

## Escopo
Teste de persistencia de perfil de jogador no harness [../../Assets/Scripts/Testing/PlayerDataTester.cs](../../Assets/Scripts/Testing/PlayerDataTester.cs).

## Dependencias
- PlayerDataService: [../../Assets/Scripts/Services/PlayerDataService.cs](../../Assets/Scripts/Services/PlayerDataService.cs)
- Modelo: [../../Assets/Scripts/Models/PlayerProfileData.cs](../../Assets/Scripts/Models/PlayerProfileData.cs)
- PlayFabService autenticado

## Objetivo Tecnico
Validar:
- Carga de profile do UserData.
- Salvamento de progresso (level, xp, settings).
- Reset de dados de teste para baseline previsivel.

## Atalhos
- Tecla 1: carregar dados
- Tecla 2: salvar profile exemplo
- Tecla 3: resetar dados
- Tecla 4: adicionar 100 XP ao profile atual

## Fluxo Detalhado
1. Service tenta auto-load no Start quando login ja existe.
2. Load usa `GetUserData` com chave `player_profile`.
3. Se nao houver valor, cria profile default.
4. Save serializa profile para JSON e envia com `UpdateUserData`.
5. Eventos notificam tester para feedback.

## Como Usar
1. Pressionar 1 para carregar estado inicial.
2. Pressionar 2 para escrever profile de referencia.
3. Pressionar 4 para testar mutacao incremental de XP.
4. Pressionar 3 para restaurar baseline.

## Indicadores De Sucesso
- `✅ Profile carregado` com valores esperados.
- `✅ Profile salvo` apos update.

## Uso Do Service
Metodos:
- `LoadPlayerData()`
- `SaveProgress(level, currentXp, settings)`
- `ResetForTests()`

Propriedade:
- `CurrentProfile`

Eventos:
- `OnPlayerDataLoaded`
- `OnPlayerDataSaved`
- `OnPlayerDataFailed`

## Observacoes
- O service preserva cache local para leitura rapida no client.
- Para reproducibilidade, usar `ResetForTests` antes de cenarios comparativos.
