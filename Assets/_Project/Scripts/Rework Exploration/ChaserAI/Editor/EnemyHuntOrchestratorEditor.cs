using UnityEditor;

/// <summary>
/// Sem estado próprio para mostrar em Play Mode (o orquestrador só repassa
/// o evento OnPreyCaught) - mantido apenas para consistência com os outros
/// editores do pacote, caso vire a ganhar algo a exibir no futuro.
/// </summary>
[CustomEditor(typeof(EnemyHuntOrchestrator))]
public class EnemyHuntOrchestratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }
}
