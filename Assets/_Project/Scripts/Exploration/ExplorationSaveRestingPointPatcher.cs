using Services.DebugUtilities;
using UnityEngine;

/// <summary>
/// Substitui <c>Position</c>/<c>Rotation</c> de cada snapshot pelo
/// <c>RestingPoint</c> do personagem na cena e reposiciona os personagens.
///
/// O fluxo correcto não passa pelo <c>RestoreState</c> público pois ele
/// relê o disco quando <c>_hasSave</c> está false (resetado após cada apply).
/// Em vez disso, chama <c>ApplySnapshotsAndSave</c> que aplica directamente
/// os snapshots já modificados em memória e persiste em seguida.
/// </summary>
public sealed class ExplorationSaveRestingPointPatcher : MonoBehaviour
{
    [Tooltip("Se verdadeiro, o patch é aplicado automaticamente ao OnEnable.")]
    [SerializeField] private bool _patchOnEnable = true;

    private void OnEnable()
    {
        if (_patchOnEnable)
            Patch();
    }

    public void Patch()
    {
        var ctx = ExplorationLoadContext.Instance;

        if (ctx == null)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                "[RestingPointPatcher] ExplorationLoadContext.Instance é nulo.",
                LogCategory.Player);
            return;
        }

        var snapshots = ctx.Snapshots;

        if (snapshots == null || snapshots.Count == 0)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[RestingPointPatcher] Nenhum snapshot disponível.",
                LogCategory.Player);
            return;
        }

        var patchedSnapshotCount = ctx.MoveSnapshotsToCharacterRestingPoints();
        if (patchedSnapshotCount == 0)
        {
            LoggerService.PrintLogMessage(LogLevel.Warning,
                "[RestingPointPatcher] Nenhum snapshot foi actualizado (RestingPoint em falta?).",
                LogCategory.Player);
            return;
        }

        // Aplica directo sem passar pelo LoadFromFileAsync
        ctx.ApplySnapshotsAndSave();

        LoggerService.PrintLogMessage(LogLevel.Debug,
            $"[RestingPointPatcher] Patch aplicado em {patchedSnapshotCount} snapshot(s).",
            LogCategory.Player);
    }
}