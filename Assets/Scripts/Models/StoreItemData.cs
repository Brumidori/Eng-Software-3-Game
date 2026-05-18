using System;
using System.Collections.Generic;

[Serializable]
public class StoreItemData
{
    public string itemId;
    public string displayName;
    public string description;
    public string iconKey;
    public string iconUrl;
    public int price;
    public string virtualCurrency;
    public string storeId;
    public List<string> tags = new List<string>();
}