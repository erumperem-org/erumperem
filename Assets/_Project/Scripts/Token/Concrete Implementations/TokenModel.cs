using System;
using UnityEngine;
using Core.Tokens;
using UnityEngine.AddressableAssets;
[Serializable]
public class TokenModel
{
    [SerializeField]
    public string tokenDisplayName;
    
    [SerializeField]
    private string _tokenStackingStrategyString;

    [SerializeField]
    private string _tokenAllocationStyleString;

    [SerializeField]

    public string tokenMaterialAddress;
    public readonly string tokenModelAddress = "Assets/_Project/Prefabs/TokenBase.prefab";
    public ITokenAllocationStyle TokenAllocationStyle;
    public TokenStackData tokenStackingdata;
    public TokenModel(String displayName, TokenStackData Stackingdata, ITokenAllocationStyle allocationStyle, string tokenMaterialAddress)
    {
        this.tokenDisplayName = displayName;
        this.tokenStackingdata = Stackingdata;
        this.TokenAllocationStyle = allocationStyle;
        this.tokenMaterialAddress = tokenMaterialAddress;
        this._tokenStackingStrategyString = Stackingdata.GetType().Name.ToString();
        this._tokenAllocationStyleString = allocationStyle.GetType().Name.ToString();
    }
}