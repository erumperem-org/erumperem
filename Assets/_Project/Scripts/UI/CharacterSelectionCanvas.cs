using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Canvas dinâmico de seleção de personagem.
///
/// Uso:
///   1. Crie um Canvas filho do NPC de interação.
///   2. Arraste este componente para o Canvas.
///   3. Preencha as referências no Inspector.
///   4. Chame Open(character) quando o jogador interagir com o NPC.
///
/// Layout esperado:
///   Canvas
///   └── Panel
///       ├── TxtCharacterName   (TextMeshProUGUI)
///       ├── TxtCurrentState    (TextMeshProUGUI)
///       ├── BtnSetMain         (Button)
///       ├── BtnSetCompanion    (Button)
///       └── BtnClose           (Button)
/// </summary>
public sealed class CharacterSelectionCanvas : MonoBehaviour
{
    [Header("Referências de UI")]
    [SerializeField] private GameObject            _panel;
    [SerializeField] private TextMeshProUGUI       _txtCharacterName;
    [SerializeField] private TextMeshProUGUI       _txtCurrentState;
    [SerializeField] private Button                _btnSetMain;
    [SerializeField] private Button                _btnSetCompanion;
    [SerializeField] private Button                _btnClose;

    [Header("Textos dos botões (opcional)")]
    [SerializeField] private TextMeshProUGUI       _btnMainLabel;
    [SerializeField] private TextMeshProUGUI       _btnCompanionLabel;

    [Header("Dependências")]
    [SerializeField] private PlayableCharactersManager _manager;

    // ── Estado interno ────────────────────────────────────────────────────

    private PlayableCharacter _current;

    // ── Unity lifecycle ───────────────────────────────────────────────────

    private void Awake()
    {
        // Registra listeners uma única vez
        _btnSetMain.onClick.AddListener(OnClickSetMain);
        _btnSetCompanion.onClick.AddListener(OnClickSetCompanion);
        _btnClose.onClick.AddListener(Close);

        _panel.SetActive(false);
    }

    private void OnDestroy()
    {
        _btnSetMain.onClick.RemoveListener(OnClickSetMain);
        _btnSetCompanion.onClick.RemoveListener(OnClickSetCompanion);
        _btnClose.onClick.RemoveListener(Close);
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>
    /// Abre o canvas configurado para o personagem informado.
    /// Chamado pelo NPC de interação (ex: PlayerNpcInteraction.cs).
    /// </summary>
    public void Open(PlayableCharacter character)
    {
        if (character == null) return;

        _current = character;
        Refresh();
        _panel.SetActive(true);
    }

    public void Close()
    {
        _panel.SetActive(false);
        _current = null;
    }

    // ── Handlers de botão ─────────────────────────────────────────────────

    private void OnClickSetMain()
    {
        if (_current == null) return;
        _manager.SetState(PlayableCharacterState.Main, _current);
        Close();
    }

    private void OnClickSetCompanion()
    {
        if (_current == null) return;
        _manager.SetState(PlayableCharacterState.Companion, _current);
        Close();
    }

    // ── Refresh ───────────────────────────────────────────────────────────

    /// <summary>
    /// Atualiza todos os elementos visuais de acordo com o estado atual do personagem.
    /// </summary>
    private void Refresh()
    {
        if (_current == null) return;

        // Nome e estado atual
        //_txtCharacterName.text = _current.characterName;
        _txtCurrentState.text  = $"Estado atual: {_current.CurrentState}";

        // Desabilita o botão correspondente ao estado atual (não faz sentido promover para o mesmo estado)
        _btnSetMain.interactable      = _current.CurrentState != PlayableCharacterState.Main;
        _btnSetCompanion.interactable = _current.CurrentState != PlayableCharacterState.Companion;

        // Labels dinâmicos (opcional — só se os TextMeshPro dos botões foram atribuídos)
        if (_btnMainLabel      != null) _btnMainLabel.text      = "Definir como Main";
        if (_btnCompanionLabel != null) _btnCompanionLabel.text = "Definir como Companion";
    }
}
