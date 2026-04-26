using System.Collections.Generic;
using Core.Tokens;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;

namespace Services.AddressablesSystem
{
    public class AddressablesService
    {
        public void Spawn(string prefabAddress, string materialAddress, Vector3 position)
        {
            var prefabRef = new AssetReferenceGameObject(prefabAddress);
            var materialRef = new AssetReferenceT<Material>(materialAddress);

            prefabRef.InstantiateAsync(position, Quaternion.identity).Completed += prefabOp =>
            {
                if (prefabOp.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError($"Failed to spawn prefab: {prefabAddress}");
                    return;
                }

                GameObject instance = prefabOp.Result;

                materialRef.LoadAssetAsync().Completed += matOp =>
                {
                    if (matOp.Status != AsyncOperationStatus.Succeeded)
                    {
                        Debug.LogError($"Failed to load material: {materialAddress}");
                        return;
                    }

                    var renderer = instance.GetComponent<Renderer>();
                    if (renderer != null)
                        renderer.material = matOp.Result;
                };
            };
        }

        public async Task<GameObject> SpawnReturningObject(string objectName, string prefabAddress, string materialAddress, Vector3 position, Quaternion rotation, Transform parent)
        {
            var prefabRef = new AssetReferenceGameObject(prefabAddress);
            var materialRef = new AssetReferenceT<Material>(materialAddress);

            var prefabOp = prefabRef.InstantiateAsync(position, rotation);
            GameObject instance = await prefabOp.Task;
            instance.name = objectName;
            instance.transform.SetParent(parent);
            
            if (prefabOp.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to spawn prefab: {prefabAddress}");
                return null;
            }

            var matOp = materialRef.LoadAssetAsync();
            Material mat = await matOp.Task;

            if (matOp.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"Failed to load material: {materialAddress}");
                return instance;
            }

            var renderer = instance.GetComponent<Renderer>();
            if (renderer != null)
                renderer.material = mat;

            return instance;
        }

        public void Destroy(GameObject instance)
        {
            if (instance == null) return;

            Addressables.ReleaseInstance(instance);
        }
    }
}