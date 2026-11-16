#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Erumperem.Editor.Combat
{
    /// <summary>
    /// No BeaconOfHopeNew o globo ocular (osso OlhoBola) partilha o material da pele.
    /// Na importação, move os triângulos dominados por esse osso para um submesh próprio
    /// e atribui-lhe o material branco brilhante <c>OlhoBolaMat</c>.
    /// </summary>
    public sealed class BeaconOfHopeEyePostprocessor : AssetPostprocessor
    {
        private const string ModelPath = "Assets/Characters/BeaconOfHopeNew/BeaconOfHopeNew.fbx";
        private const string EyeMaterialPath = "Assets/Characters/BeaconOfHopeNew/OlhoBolaMat.mat";
        private const string EyeBoneName = "OlhoBola";

        public override uint GetVersion() => 1;

        private void OnPreprocessModel()
        {
            if (assetPath != ModelPath) return;
            context.DependsOnSourceAsset(EyeMaterialPath);
        }

        private void OnPostprocessModel(GameObject root)
        {
            if (assetPath != ModelPath) return;

            var eyeMaterial = AssetDatabase.LoadAssetAtPath<Material>(EyeMaterialPath);
            if (eyeMaterial == null)
            {
                context.LogImportWarning($"Material do olho não encontrado em {EyeMaterialPath}.");
                return;
            }

            foreach (var skinnedRenderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                SplitEyeSubmesh(skinnedRenderer, eyeMaterial);
            }
        }

        private void SplitEyeSubmesh(SkinnedMeshRenderer skinnedRenderer, Material eyeMaterial)
        {
            var mesh = skinnedRenderer.sharedMesh;
            if (mesh == null) return;

            var eyeBoneIndex = Array.FindIndex(skinnedRenderer.bones, bone => bone != null && bone.name == EyeBoneName);
            if (eyeBoneIndex < 0) return;

            var boneWeights = mesh.boneWeights;
            var isEyeVertex = new bool[boneWeights.Length];
            for (var i = 0; i < boneWeights.Length; i++)
            {
                isEyeVertex[i] = boneWeights[i].boneIndex0 == eyeBoneIndex && boneWeights[i].weight0 > 0.5f;
            }

            var keptTrianglesPerSubmesh = new List<int[]>(mesh.subMeshCount);
            var eyeTriangles = new List<int>();
            for (var submesh = 0; submesh < mesh.subMeshCount; submesh++)
            {
                var triangles = mesh.GetTriangles(submesh);
                var kept = new List<int>(triangles.Length);
                for (var t = 0; t < triangles.Length; t += 3)
                {
                    var target = isEyeVertex[triangles[t]] && isEyeVertex[triangles[t + 1]] && isEyeVertex[triangles[t + 2]]
                        ? eyeTriangles
                        : kept;
                    target.Add(triangles[t]);
                    target.Add(triangles[t + 1]);
                    target.Add(triangles[t + 2]);
                }

                keptTrianglesPerSubmesh.Add(kept.ToArray());
            }

            if (eyeTriangles.Count == 0)
            {
                context.LogImportWarning($"Nenhum triângulo pesado no osso {EyeBoneName} em {mesh.name}.");
                return;
            }

            mesh.subMeshCount = keptTrianglesPerSubmesh.Count + 1;
            for (var submesh = 0; submesh < keptTrianglesPerSubmesh.Count; submesh++)
            {
                mesh.SetTriangles(keptTrianglesPerSubmesh[submesh], submesh, false);
            }

            mesh.SetTriangles(eyeTriangles, keptTrianglesPerSubmesh.Count, false);

            var materials = new List<Material>(skinnedRenderer.sharedMaterials) { eyeMaterial };
            skinnedRenderer.sharedMaterials = materials.ToArray();
        }
    }
}
#endif
