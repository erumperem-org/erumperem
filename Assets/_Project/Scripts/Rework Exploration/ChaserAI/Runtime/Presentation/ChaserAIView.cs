using System;
using System.Collections.Generic;
using BarSystem.Bars.Corruption;
using UnityEngine;

/// <summary>
/// Mostra o modelo do chaser correspondente ao tier de corrupção atual (comportamento original).
/// Ajustes para populações grandes: a busca pelo CorruptionBarInstaller é feita uma única vez
/// para todos os chasers (cache estático) em vez de uma por instância, e o tier é reaplicado
/// ao ativar o objeto, para não ficar com o modelo errado se o tier mudou enquanto ele estava desativado.
/// </summary>
public class ChaserAIView : MonoBehaviour
{
    [Serializable]
    public struct ChaserAiModelView
    {
        public GameObject view;
        public int tier;
    }

    public CorruptionBarInstaller corruptionBarInstaller;
    public List<ChaserAiModelView> chaserAiModelViews;

    private static CorruptionBarInstaller sharedInstaller;

    private void OnEnable()
    {
        ResolveInstaller();

        if (corruptionBarInstaller == null)
        {
            return;
        }

        corruptionBarInstaller.OnTierChanged += HandleChaserAITierDependentView;
        HandleChaserAITierDependentView(corruptionBarInstaller.CurrentTier);
    }

    private void Start()
    {
        // Mantido do original: o instalador pode só estar pronto depois de todos os Awake.
        ResolveInstaller();

        if (corruptionBarInstaller != null)
        {
            HandleChaserAITierDependentView(corruptionBarInstaller.CurrentTier);
        }
    }

    private void OnDisable()
    {
        if (corruptionBarInstaller != null)
        {
            corruptionBarInstaller.OnTierChanged -= HandleChaserAITierDependentView;
        }
    }

    private void ResolveInstaller()
    {
        if (corruptionBarInstaller != null)
        {
            return;
        }

        if (sharedInstaller == null)
        {
            sharedInstaller = FindAnyObjectByType<CorruptionBarInstaller>();
        }

        corruptionBarInstaller = sharedInstaller;
    }

    private void HandleChaserAITierDependentView(int tier)
    {
        foreach (var views in chaserAiModelViews)
        {
            if (views.view != null)
            {
                views.view.SetActive(views.tier == tier);
            }
        }
    }
}
