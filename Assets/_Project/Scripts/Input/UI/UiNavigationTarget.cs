using UnityEngine;
using UnityEngine.UI;

public sealed class UiNavigationTarget
{
    public UiNavigationTarget(GameObject gameObject, RectTransform rectTransform, Selectable selectable, bool isCustomUiButton)
    {
        GameObject = gameObject;
        RectTransform = rectTransform;
        Selectable = selectable;
        IsCustomUiButton = isCustomUiButton;
    }

    public GameObject GameObject { get; }
    public RectTransform RectTransform { get; }
    public Selectable Selectable { get; }
    public bool IsCustomUiButton { get; }
}
