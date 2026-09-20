using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Gerencia o feedback sonoro de hover e seleção de personagens (aliados e inimigos).
/// 
/// Arquitetura de Canais Independentes:
/// 1. Canal Mouse: Responde a OnMouseEnter/OnPointerEnter com histerese de saída (ignora oscilações de Idle na borda).
/// 2. Canal Teclado: Acionado explicitamente pelo CombatInputController quando o jogador navega entre alvos.
/// 
/// Elimina:
/// - Loops quando o mouse está no vazio e o teclado seleciona um alvo.
/// - Conflitos de desseleção do EventSystem.
/// - Efeito "metralhadora" nas bordas de colisores e sobreposições.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class CharacterHoverAudio : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Identificação do Som")]
    [Tooltip("Nome exato do som registrado no AudioManager.")]
    [SerializeField] private string _hoverSound = "CharacterHover";

    [Tooltip("Volume multiplicador ao reproduzir este efeito.")]
    [Range(0f, 1f)]
    [SerializeField] private float _volume = 1f;

    [Header("Filtros Anti-Spam (Mouse)")]
    [Tooltip("Janela de tolerância para ignorar oscilações de Idle na borda. Se o mouse sair e reentrar no mesmo personagem dentro deste tempo, nenhum som é repetido.")]
    [Min(0.05f)]
    [SerializeField] private float _mouseReenterGracePeriod = 0.35f;

    [Tooltip("Tempo mínimo de retenção antes de permitir que o mouse troque de um personagem para outro vizinho sobreposto.")]
    [Min(0.05f)]
    [SerializeField] private float _switchLockDuration = 0.18f;

    [Tooltip("Cadência mínima global absoluta entre qualquer som de hover no jogo.")]
    [Min(0.01f)]
    [SerializeField] private float _globalMinInterval = 0.06f;

    // ── Canal 1: Estado Independente do Mouse ─────────────────────────────
    private static int s_currentMouseTargetId = 0;
    private static int s_lastMouseTargetId = 0;
    private static float s_lastMouseExitTime = -1f;
    private static float s_mouseSwitchLockUntil = -1f;

    // ── Canal 2: Estado Independente do Teclado/Gamepad ──────────────────
    private static int s_currentKeyboardTargetId = 0;

    // ── Barramento Global de Áudio ───────────────────────────────────────
    private static float s_lastGlobalPlayTime = -1f;

    // Cache local do ID do personagem raiz
    private int _cachedTargetId;
    private bool _hasCachedId;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_currentMouseTargetId = 0;
        s_lastMouseTargetId = 0;
        s_lastMouseExitTime = -1f;
        s_mouseSwitchLockUntil = -1f;
        s_currentKeyboardTargetId = 0;
        s_lastGlobalPlayTime = -1f;
    }

    private void Awake()
    {
        _cachedTargetId = ResolveTargetId();
        _hasCachedId = true;
    }

    // ── Eventos de Mouse (Físico 3D) ─────────────────────────────────────

    private void OnMouseEnter()
    {
        HandleMouseHoverEnter();
    }

    private void OnMouseExit()
    {
        HandleMouseHoverExit();
    }

    // ── Eventos de Mouse (PhysicsRaycaster / EventSystem) ────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        HandleMouseHoverEnter();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        HandleMouseHoverExit();
    }

    private void HandleMouseHoverEnter()
    {
        // Se CombatHoverFocusMarker existir na cena, ele é o coordenador central
        // do áudio de hover (mouse + teclado), evitando sons duplicados no combate.
        if (FindFirstObjectByType<Erumperem.Combat.CombatHoverFocusMarker>() != null)
        {
            return;
        }

        float now = Time.unscaledTime;
        int targetId = _hasCachedId ? _cachedTargetId : ResolveTargetId();

        // Se o mouse já está sobre este alvo registrado, não toca novamente
        if (s_currentMouseTargetId == targetId)
        {
            return;
        }

        // Histerese de Borda: se saiu deste mesmo alvo há menos de 0.35s (animação de Idle), ignora!
        if (s_lastMouseTargetId == targetId && (now - s_lastMouseExitTime) < _mouseReenterGracePeriod)
        {
            s_currentMouseTargetId = targetId;
            return;
        }

        // Trava de troca rápida entre personagens sobrepostos
        if (s_currentMouseTargetId != 0 && s_currentMouseTargetId != targetId)
        {
            if (now < s_mouseSwitchLockUntil)
            {
                return;
            }
        }

        // Cadência global mínima (evita estalos em cliques rápidos)
        if (now - s_lastGlobalPlayTime < _globalMinInterval)
        {
            return;
        }

        // Aplica o novo foco do mouse
        s_currentMouseTargetId = targetId;
        s_lastMouseTargetId = targetId;
        s_mouseSwitchLockUntil = now + _switchLockDuration;
        s_lastGlobalPlayTime = now;

        PlayHoverSound();
    }

    private void HandleMouseHoverExit()
    {
        int targetId = _hasCachedId ? _cachedTargetId : ResolveTargetId();

        if (s_currentMouseTargetId == targetId)
        {
            s_currentMouseTargetId = 0;
            s_lastMouseTargetId = targetId;
            s_lastMouseExitTime = Time.unscaledTime;
        }
    }

    // ── Canal do Teclado (Invocado pelo CombatInputController) ───────────

    /// <summary>
    /// Acionado quando o jogador navega até um alvo via teclado ou gamepad.
    /// Opera de forma totalmente desacoplada da posição do mouse.
    /// </summary>
    public static void TriggerKeyboardHover(GameObject target)
    {
        if (target == null) return;

        CharacterHoverAudio hoverAudio = target.GetComponentInChildren<CharacterHoverAudio>();
        if (hoverAudio != null)
        {
            hoverAudio.NotifyKeyboardSelected();
        }
    }

    /// <summary>
    /// Limpa o foco do teclado quando a fase de seleção de alvos é cancelada ou encerrada.
    /// </summary>
    public static void ClearKeyboardFocus()
    {
        s_currentKeyboardTargetId = 0;
    }

    public void NotifyKeyboardSelected()
    {
        float now = Time.unscaledTime;
        int targetId = _hasCachedId ? _cachedTargetId : ResolveTargetId();

        // REGRA DE OURO DO TECLADO: se já é o alvo focado pelo teclado, NUNCA repete!
        if (s_currentKeyboardTargetId == targetId)
        {
            return;
        }

        // Cadência global mínima
        if (now - s_lastGlobalPlayTime < _globalMinInterval)
        {
            return;
        }

        s_currentKeyboardTargetId = targetId;
        s_lastGlobalPlayTime = now;

        PlayHoverSound();
    }

    private void PlayHoverSound()
    {
        if (AudioManager.instance != null && !string.IsNullOrEmpty(_hoverSound))
        {
            AudioManager.instance.PlaySFX(_hoverSound, _volume);
        }
    }

    /// <summary>
    /// Garante que membros, armas e tronco apontem para o mesmo ID da raiz do modelo.
    /// </summary>
    private int ResolveTargetId()
    {
        var animator = GetComponentInParent<Animator>();
        if (animator != null)
        {
            return animator.gameObject.GetInstanceID();
        }

        var rb = GetComponentInParent<Rigidbody>();
        if (rb != null)
        {
            return rb.gameObject.GetInstanceID();
        }

        return transform.root.gameObject.GetInstanceID();
    }

    // Compatibilidade reversa
    public void NotifyTargetSelected()
    {
        NotifyKeyboardSelected();
    }

    public static void TriggerFor(GameObject target)
    {
        TriggerKeyboardHover(target);
    }
}

//esse aqui é meu mesmo ent ta de boa eu fazer oq eu quiser nele muahaha