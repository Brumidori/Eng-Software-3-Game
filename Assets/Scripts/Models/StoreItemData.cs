using System;
using System.Collections.Generic;

[Serializable]
public class StoreItemData
{
    public string itemId;
    public string displayName;
    public string description;
    public int price;
    public string virtualCurrency;
    public List<string> tags = new List<string>();
}