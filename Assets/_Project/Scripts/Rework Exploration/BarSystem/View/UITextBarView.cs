using UnityEngine;
using UnityEngine.UI;
using BarSystem.Core;
using TMPro;

namespace BarSystem.View
{
    /// <summary>
    /// Exibe o valor normalizado (0-1) que chega até ela como texto, ex.: "42%".
    /// Recebe o valor já processado pela cadeia de views (ex.: já passado pelo
    /// HalfScaleBarView), então mostra exatamente o que está sendo desenhado
    /// na barra - não o valor real interno do BarModel.
    /// </summary>
    public class UITextBarView : MonoBehaviour, IBarView
    {
        [SerializeField] private TMP_Text _label; // troque por TMP_Text se o projeto usar TextMeshPro
        [SerializeField] private string _format = "{0:0}%";

        public void SetNormalizedValue(float normalizedValue)
        {
            if (_label == null) return;
            _label.text = string.Format(_format, normalizedValue * 100f);
        }
    }
}