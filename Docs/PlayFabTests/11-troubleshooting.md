# Troubleshooting PlayFab Tests

## Objetivo
Catalogar erros recorrentes observados durante os testes e orientar diagnostico rapido.

## Matriz De Erros Comuns

1. Erro: `/Client/LoginWithCustomID: User not found`
Possiveis causas:
- CustomId nao existe no TitleId atual.
- Ambientes diferentes (Dev/Staging/Prod).
- Erro de digitacao em user ID.
- Valor antigo serializado no Inspector.
Diagnostico:
- Confirmar `TitleId` no log do MatchmakingService/PlayFabService.
- Confirmar valor efetivo logado pelo tester no Start.
- Validar no GameManager/Inspector se o campo foi sobrescrito por valor antigo.
Acao:
- Corrigir ID.
- Migrar valor legado com `OnValidate` quando aplicavel.
- Em teste controlado, habilitar create missing users (quando apropriado).

2. Erro: `/Client/PurchaseItem: WrongVirtualCurrency`
Possiveis causas:
- Compra enviada com moeda diferente da configurada no item da loja.
- Preco enviado nao corresponde ao preco do item na loja.
- Item nao carregado da store correta.
Diagnostico:
- Carregar store antes da compra.
- Confirmar no log moeda e preco efetivos enviados no request.
- Confirmar `StoreId` correto na carga e compra.
Acao:
- Usar moeda/preco vindos do item carregado.
- Garantir que `StoreId` esteja presente no fluxo de compra.

3. Erro: Matchmaking falha sem criar match
Possiveis causas:
- QueueName incorreta.
- User IDs invalidos.
- Timeout curto.
- Tickets antigos interferindo no fluxo.
Diagnostico:
- Conferir log inicial de config (queue, users, timeout, TitleId).
- Usar resumo de diagnostico no tester.
- Validar status de tickets em polling.
Acao:
- Limpar tickets ativos (`CancelAllMatchmakingTicketsForPlayer`).
- Aumentar timeout e revisar queue no portal.

4. Sintoma: valor de queue/user volta para antigo mesmo apos alterar codigo
Possiveis causas:
- Campo serializado do componente no Inspector sobrescreve default do script.
Diagnostico:
- Ver log de config do tester em runtime.
Acao:
- Atualizar campo no Inspector.
- Aplicar migracao via `OnValidate` para valores legados.

5. Erro: CloudScript Economy sem retorno esperado
Possiveis causas:
- Funcao nao publicada no Title.
- Nome de funcao divergente no Inspector do EconomyService.
- Revision Live desatualizada.
Diagnostico:
- Ver erro `No function named` no log do EconomyService.
- Confirmar funcoes no arquivo [../PlayFab/CloudScriptEconomy.js](../PlayFab/CloudScriptEconomy.js).
Acao:
- Publicar funcoes corretas.
- Ajustar nomes no service.
- Testar com `useLatestRevision=true`.

## Checklist Rapido Antes De Rodar Qualquer Tester
1. Confirmar login concluido no PlayFabService.
2. Confirmar TitleId do ambiente esperado.
3. Confirmar campos serializados no Inspector.
4. Confirmar queue/store/item/currency corretos para o teste.
5. Limpar estado residual (tickets, caches) quando necessario.
