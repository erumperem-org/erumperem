#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Core.Storage.Editor
{
    [CustomEditor(typeof(StorageStrategyTestbed))]
    public sealed class StorageStrategyTestbedEditor : UnityEditor.Editor
    {
        private int _simulatedCurrentInSlot;
        private int _simulatedTotalInstances;
        private int _simulatedAddAmount = 1;

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            var testbed = (StorageStrategyTestbed)target;
            var strategy = testbed.Strategy;

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Dados Resolvidos", EditorStyles.boldLabel);

            if (strategy == null)
            {
                EditorGUILayout.HelpBox("Nenhuma estratégia atribuída.", MessageType.Warning);
                return;
            }

            EditorGUILayout.LabelField("CanShareSlot", strategy.CanShareSlot.ToString());
            EditorGUILayout.LabelField("MaxPerSlot", strategy.MaxPerSlot?.ToString() ?? "Ilimitado");
            EditorGUILayout.LabelField("MaxTotalInstances", strategy.MaxTotalInstances?.ToString() ?? "Ilimitado");

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Simular Adição", EditorStyles.boldLabel);

            _simulatedCurrentInSlot = EditorGUILayout.IntField("Quantidade atual no slot", _simulatedCurrentInSlot);
            _simulatedTotalInstances = EditorGUILayout.IntField("Total já existente (todos os slots)", _simulatedTotalInstances);
            _simulatedAddAmount = EditorGUILayout.IntField("Quantidade a adicionar", _simulatedAddAmount);

            if (GUILayout.Button("Simular"))
                SimulateAdd(strategy);
        }

        private void SimulateAdd(IStorageStrategy strategy)
        {
            int perSlotCap = strategy.MaxPerSlot ?? int.MaxValue;
            int totalCap = strategy.MaxTotalInstances ?? int.MaxValue;

            int spaceInSlot = Mathf.Max(0, perSlotCap - _simulatedCurrentInSlot);
            int spaceTotal = Mathf.Max(0, totalCap - _simulatedTotalInstances);
            int allowed = Mathf.Min(_simulatedAddAmount, spaceInSlot, spaceTotal);

            if (allowed <= 0)
                Debug.Log($"[StorageStrategyTestbed] Não cabe nenhuma unidade. Espaço no slot: {spaceInSlot}, espaço total: {spaceTotal}.");
            else if (allowed < _simulatedAddAmount)
                Debug.Log($"[StorageStrategyTestbed] Split parcial: {allowed}/{_simulatedAddAmount} unidade(s) caberiam.");
            else
                Debug.Log($"[StorageStrategyTestbed] Todas as {allowed} unidade(s) caberiam.");
        }
    }
}
#endif
