using System;
using PlayFab;
using PlayFab.ClientModels;
using PlayFab.CloudScriptModels;

public sealed class PlayFabClientFacade : IPlayFabClientFacade
{
    public void LoginWithCustomID(LoginWithCustomIDRequest request, Action<LoginResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.LoginWithCustomID(request, successCallback, errorCallback);
    }

    public void LoginWithEmailAddress(LoginWithEmailAddressRequest request, Action<LoginResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.LoginWithEmailAddress(request, successCallback, errorCallback);
    }

    public void GetTitleData(GetTitleDataRequest request, Action<GetTitleDataResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.GetTitleData(request, successCallback, errorCallback);
    }

    public void GetUserData(GetUserDataRequest request, Action<GetUserDataResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.GetUserData(request, successCallback, errorCallback);
    }

    public void UpdateUserData(UpdateUserDataRequest request, Action<UpdateUserDataResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.UpdateUserData(request, successCallback, errorCallback);
    }

    public void UpdatePlayerStatistics(UpdatePlayerStatisticsRequest request, Action<UpdatePlayerStatisticsResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.UpdatePlayerStatistics(request, successCallback, errorCallback);
    }

    public void GetLeaderboard(GetLeaderboardRequest request, Action<GetLeaderboardResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.GetLeaderboard(request, successCallback, errorCallback);
    }

    public void GetUserInventory(GetUserInventoryRequest request, Action<GetUserInventoryResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.GetUserInventory(request, successCallback, errorCallback);
    }

    public void AddUserVirtualCurrency(AddUserVirtualCurrencyRequest request, Action<ModifyUserVirtualCurrencyResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.AddUserVirtualCurrency(request, successCallback, errorCallback);
    }

    public void SubtractUserVirtualCurrency(SubtractUserVirtualCurrencyRequest request, Action<ModifyUserVirtualCurrencyResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.SubtractUserVirtualCurrency(request, successCallback, errorCallback);
    }

    public void ConsumeItem(ConsumeItemRequest request, Action<ConsumeItemResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.ConsumeItem(request, successCallback, errorCallback);
    }

    public void PurchaseItem(PurchaseItemRequest request, Action<PurchaseItemResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.PurchaseItem(request, successCallback, errorCallback);
    }

    public void GetCatalogItems(GetCatalogItemsRequest request, Action<GetCatalogItemsResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.GetCatalogItems(request, successCallback, errorCallback);
    }

    public void GetStoreItems(GetStoreItemsRequest request, Action<GetStoreItemsResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.GetStoreItems(request, successCallback, errorCallback);
    }

    public void ExecuteCloudScript(PlayFab.ClientModels.ExecuteCloudScriptRequest request, Action<PlayFab.ClientModels.ExecuteCloudScriptResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.ExecuteCloudScript(request, successCallback, errorCallback);
    }

    public void ExecuteFunction(ExecuteFunctionRequest request, Action<ExecuteFunctionResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabCloudScriptAPI.ExecuteFunction(request, successCallback, errorCallback);
    }

    public void RegisterPlayFabUser(RegisterPlayFabUserRequest request, Action<RegisterPlayFabUserResult> successCallback, Action<PlayFabError> errorCallback)
    {
        PlayFabClientAPI.RegisterPlayFabUser(request, successCallback, errorCallback);
    }
}