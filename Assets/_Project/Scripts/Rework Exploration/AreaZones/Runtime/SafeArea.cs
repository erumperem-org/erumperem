using UnityEngine;

/// <summary>
/// Define o escopo de uma área onde um alvo está "seguro" (fora de
/// perseguição, fora de perigo, etc.) - apenas delimita a região via
/// <see cref="CircularZone.Contains"/>, sem nenhum comportamento reativo.
///
/// Sistemas que precisam saber quando um alvo entra/sai devem usar uma
/// subclasse com esse comportamento (ver <see cref="Hub"/>) ou consultar
/// <see cref="CircularZone.Contains"/> diretamente por conta própria.
/// Pode haver mais de uma SafeArea no mundo simultaneamente.
/// </summary>
public class SafeArea : CircularZone
{
    protected override Color GizmoColor => Color.green;
}
