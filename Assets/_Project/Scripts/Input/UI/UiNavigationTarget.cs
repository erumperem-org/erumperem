
using UnityEngine;
using UnityEngine.UI;

public sealed class UiNavigationTarget
{
    public UiNavigationTarget(GameObject gameObject, RectTransform rectTransform, Selectable selectable, bool isCustomUiButton, bool isScrollRect = false)
    {
        GameObject = gameObject;
        RectTransform = rectTransform;
        Selectable = selectable;
        IsCustomUiButton = isCustomUiButton;
        IsScrollRect = isScrollRect;
    }

    public GameObject GameObject { get; }
    public RectTransform RectTransform { get; }
    public Selectable Selectable { get; }
    public bool IsCustomUiButton { get; }
    public bool IsScrollRect { get; }
}
