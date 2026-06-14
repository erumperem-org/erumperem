using System;
using UnityEngine;

[Serializable]
/// <summary>
/// Barra de vida de um personagem jogável.
/// Gerencia HP máximo, HP corrente e dispara eventos de mudança e morte.
/// </summary>
public sealed class PlayableHealthBar
{
    // ── Eventos ───────────────────────────────────────────────────────────

    /// <summary>
    /// Disparado sempre que o HP muda.
    /// Parâmetros: (hpAnterior, hpAtual, hpMáximo)
    /// </summary>
    public event Action<float, float, float> OnHealthChanged;

    /// <summary>
    /// Disparado uma única vez quando o HP chega a zero.
    /// </summary>
    public event Action OnHealthEmpty;

    // ── Propriedades ──────────────────────────────────────────────────────

    [SerializeField] public float MaxHealth;
    [SerializeField] public float CurrentHealth;

    /// <summary>HP normalizado entre 0 e 1 (útil para preencher a barra visualmente).</summary>
    public float NormalizedHealth => MaxHealth > 0f ? CurrentHealth / MaxHealth : 0f;

    public bool IsAlive => CurrentHealth > 0f;

    // ── Construtor ────────────────────────────────────────────────────────

    /// <param name="maxHealth">HP máximo inicial. Deve ser maior que zero.</param>
    /// <param name="startFull">Se <c>true</c>, inicia com HP cheio; caso contrário, inicia zerado.</param>
    public PlayableHealthBar(float maxHealth, bool startFull = true)
    {
        if (maxHealth <= 0f)
            throw new ArgumentOutOfRangeException(nameof(maxHealth), "HP máximo deve ser maior que zero.");

        MaxHealth     = maxHealth;
        CurrentHealth = startFull ? maxHealth : 0f;
    }

    // ── API pública ───────────────────────────────────────────────────────

    /// <summary>Aplica dano (valor positivo reduz HP).</summary>
    public void TakeDamage(float amount)
    {
        if (amount <= 0f) return;
        SetHealth(CurrentHealth - amount);
    }

    /// <summary>Aplica cura (valor positivo aumenta HP, limitado ao máximo).</summary>
    public void Heal(float amount)
    {
        if (amount <= 0f) return;
        SetHealth(CurrentHealth + amount);
    }

    /// <summary>Cura até o HP máximo.</summary>
    public void HealFull() => SetHealth(MaxHealth);

    /// <summary>Zera o HP imediatamente (dispara <see cref="OnHealthEmpty"/>).</summary>
    public void Kill() => SetHealth(0f);

    /// <summary>
    /// Redefine o HP máximo. O HP corrente é mantido proporcional se
    /// <paramref name="keepRatio"/> for <c>true</c>, ou simplesmente clamped ao novo máximo.
    /// </summary>
    public void SetMaxHealth(float newMax, bool keepRatio = false)
    {
        if (newMax <= 0f)
            throw new ArgumentOutOfRangeException(nameof(newMax), "HP máximo deve ser maior que zero.");

        float newCurrent = keepRatio
            ? newMax * NormalizedHealth
            : Mathf.Min(CurrentHealth, newMax);

        MaxHealth = newMax;
        SetHealth(newCurrent);
    }

    // ── Núcleo ────────────────────────────────────────────────────────────

    private void SetHealth(float value)
    {
        float previous = CurrentHealth;
        CurrentHealth  = Mathf.Clamp(value, 0f, MaxHealth);

        // Só dispara evento se houve mudança real.
        if (Mathf.Approximately(previous, CurrentHealth)) return;

        OnHealthChanged?.Invoke(previous, CurrentHealth, MaxHealth);

        if (CurrentHealth <= 0f)
            OnHealthEmpty?.Invoke();
    }
}