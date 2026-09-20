using System;
using System.Collections.Generic;
using BarSystem.Bars.Corruption;
using UnityEngine;

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

    void Awake()
    {
        if(corruptionBarInstaller == null)
        {
            corruptionBarInstaller = FindAnyObjectByType<CorruptionBarInstaller>();
        }
    }

    void Start()
    {
        HandleChaserAITierDependentView(corruptionBarInstaller.CurrentTier);  
    }

    void OnEnable()
    {
        corruptionBarInstaller.OnTierChanged += HandleChaserAITierDependentView;
    }

    void OnDisable()
    {
        corruptionBarInstaller.OnTierChanged -= HandleChaserAITierDependentView;
    }

    void HandleChaserAITierDependentView(int tier)
    {
        foreach(var views in chaserAiModelViews)
        {
            if(views.tier != tier)
            {
                views.view.SetActive(false);
                continue;
            }
            views.view.SetActive(true);
        }
    }
}
