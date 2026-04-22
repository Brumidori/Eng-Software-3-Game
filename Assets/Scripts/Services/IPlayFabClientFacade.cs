using System;
using System.Collections.Generic;
using PlayFab.ClientModels;

public interface IPlayFabClientFacade
{
    void LoginWithCustomID(LoginWithCustomIDRequest request, Action<LoginResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void LoginWithEmailAddress(LoginWithEmailAddressRequest request, Action<LoginResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void GetTitleData(GetTitleDataRequest request, Action<GetTitleDataResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void GetUserData(GetUserDataRequest request, Action<GetUserDataResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void UpdateUserData(UpdateUserDataRequest request, Action<UpdateUserDataResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void UpdatePlayerStatistics(UpdatePlayerStatisticsRequest request, Action<UpdatePlayerStatisticsResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void GetLeaderboard(GetLeaderboardRequest request, Action<GetLeaderboardResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void GetUserInventory(GetUserInventoryRequest request, Action<GetUserInventoryResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void AddUserVirtualCurrency(AddUserVirtualCurrencyRequest request, Action<ModifyUserVirtualCurrencyResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void SubtractUserVirtualCurrency(SubtractUserVirtualCurrencyRequest request, Action<ModifyUserVirtualCurrencyResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void ConsumeItem(ConsumeItemRequest request, Action<ConsumeItemResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void PurchaseItem(PurchaseItemRequest request, Action<PurchaseItemResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void GetCatalogItems(GetCatalogItemsRequest request, Action<GetCatalogItemsResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void GetStoreItems(GetStoreItemsRequest request, Action<GetStoreItemsResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
    void ExecuteCloudScript(ExecuteCloudScriptRequest request, Action<ExecuteCloudScriptResult> successCallback, Action<PlayFab.PlayFabError> errorCallback);
}