#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace Erumperem.Editor.Combat
{
    /// <summary>
    /// Creates the combat Animator Controller from the clips embedded in Matsuda.fbx and assigns it to her battle prefab.
    /// Run once after pulling: Erumperem/Setup Matsuda Combat Animator.
    /// </summary>
    public static class MatsudaCombatAnimatorSetup
    {
        private const string ModelPath = "Assets/Characters/Matsuda/Matsuda.fbx";
        private const string BattlePrefabPath = "Assets/_Project/Prefabs/Characters/Matsuda.prefab";
        private const string ControllerPath = "Assets/Characters/Matsuda/MatsudaAnimations/MatsudaCombatAnimator.controller";

        private const string IdleClipSuffix = "|IdleCycle";
        private const string DeathClipSuffix = "|DeathCycle";

        [MenuItem("Erumperem/Setup Matsuda Combat Animator")]
        public static void SetupMatsudaCombatAnimator()
        {
            EnsureIdleClipLoops();

            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
            if (controller == null)
            {
                controller = CreateController();
                if (controller == null)
                {
                    return;
                }
            }

            var prefabRoot = PrefabUtility.LoadPrefabContents(BattlePrefabPath);
            try
            {
                var animator = prefabRoot.GetComponent<Animator>();
                if (animator == null)
                {
                    Debug.LogError("MatsudaCombatAnimatorSetup: Animator not found on the battle prefab root.");
                    return;
                }

                animator.runtimeAnimatorController = controller;
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, BattlePrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(prefabRoot);
            }

            Debug.Log($"MatsudaCombatAnimatorSetup: {ControllerPath} assigned to {BattlePrefabPath}.");
        }

        private static void EnsureIdleClipLoops()
        {
            var modelImporter = (ModelImporter)AssetImporter.GetAtPath(ModelPath);
            var clipAnimations = modelImporter.clipAnimations.Length > 0
                ? modelImporter.clipAnimations
                : modelImporter.defaultClipAnimations;

            var idleClipAnimation = clipAnimations.FirstOrDefault(
                clipAnimation => clipAnimation.name.EndsWith(IdleClipSuffix, StringComparison.Ordinal));
            if (idleClipAnimation == null || idleClipAnimation.loopTime)
            {
                return;
            }

            idleClipAnimation.loopTime = true;
            modelImporter.clipAnimations = clipAnimations;
            modelImporter.SaveAndReimport();
        }

        private static AnimatorController CreateController()
        {
            var idleClip = FindModelClip(IdleClipSuffix);
            var deathClip = FindModelClip(DeathClipSuffix);
            if (idleClip == null || deathClip == null)
            {
                Debug.LogError($"MatsudaCombatAnimatorSetup: idle/death clips not found in {ModelPath}.");
                return null;
            }

            var controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("HitTaken", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Death", AnimatorControllerParameterType.Trigger);

            var stateMachine = controller.layers[0].stateMachine;
            var idleState = stateMachine.AddState("Idle");
            idleState.motion = idleClip;
            stateMachine.defaultState = idleState;

            var deathState = stateMachine.AddState("Death");
            deathState.motion = deathClip;

            var deathTransition = stateMachine.AddAnyStateTransition(deathState);
            deathTransition.AddCondition(AnimatorConditionMode.If, 0f, "Death");
            deathTransition.hasExitTime = false;
            deathTransition.canTransitionToSelf = false;
            deathTransition.duration = 0.1f;

            AssetDatabase.SaveAssets();
            return controller;
        }

        private static AnimationClip FindModelClip(string clipNameSuffix)
        {
            return AssetDatabase.LoadAllAssetsAtPath(ModelPath)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => clip.name.EndsWith(clipNameSuffix, StringComparison.Ordinal));
        }
    }
}
#endif
