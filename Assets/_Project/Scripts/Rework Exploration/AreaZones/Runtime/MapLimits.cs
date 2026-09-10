using UnityEngine;

/// <summary>
/// Limite externo circular do mapa. Além dele não há área patrulhável /
/// navegável para sistemas que dependem dessa fronteira (ex: geração de
/// pontos aleatórios de patrulha).
/// </summary>
public class MapLimits : CircularZone
{
    protected override Color GizmoColor => Color.yellow;
}
