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
    public string tokenLogoAddress;
    public readonly string tokenModelAddress = "Assets/_Project/Prefabs/Token (Canvas).prefab";
    public ITokenAllocationStyle TokenAllocationStyle;
    public TokenStackData tokenStackingdata;
    public Color logoColor;
    public Color backgroundColor;
    public TokenModel(
        string displayName,
        TokenStackData stackingData,
        ITokenAllocationStyle allocationStyle,
        string tokenLogoAddress,
        Color? logoColor = null,
        Color? backgroundColor = null)
    {
        this.tokenDisplayName = displayName;
        this.tokenStackingdata = stackingData;
        this.TokenAllocationStyle = allocationStyle;
        this.tokenLogoAddress = tokenLogoAddress;

        this._tokenStackingStrategyString = stackingData.GetType().Name;
        this._tokenAllocationStyleString = allocationStyle.GetType().Name;

        this.logoColor = logoColor ?? Color.white;
        this.backgroundColor = backgroundColor ?? Color.white;
    }
}