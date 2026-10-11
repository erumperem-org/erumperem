using UnityEngine;

/// <summary>
/// Assim como <see cref="MapLimits"/>, mas pensada para ser a referência de
/// mundo do mapa da UI: permite ajustar o centro efetivo da zona por um
/// offset configurável no Inspector, sem precisar mover (ou reposicionar na
/// hierarquia) o GameObject em si. Útil quando o ponto de calibração do
/// mapa não coincide exatamente com a posição de nenhum outro objeto da cena.
///
/// Se a área do minimapa for exatamente igual aos limites de patrulha já
/// definidos por MapLimits, não é necessário usar esta classe - qualquer
/// CircularZone serve como referência de mundo para PlayerMapIcon.
/// </summary>
public class WorldMapArea : CircularZone
{
    [SerializeField] private Vector3 centerOffset = Vector3.zero;

    /// <summary>Centro efetivo = posição do transform + offset ajustado no Inspector.</summary>
    public override Vector3 Center => transform.position + centerOffset;

    protected override Color GizmoColor => Color.cyan;

    /// <summary>Permite recalibrar o centro em runtime, se algum dia for necessário.</summary>
    public void SetCenterOffset(Vector3 offset) => centerOffset = offset;
}