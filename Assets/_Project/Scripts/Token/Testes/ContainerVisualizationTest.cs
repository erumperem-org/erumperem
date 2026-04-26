using Core.Tokens;
using Unity.VisualScripting;
using Unity.VisualScripting.Antlr3.Runtime;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

class ContainerVisualizationTest : MonoBehaviour
{
    TokenController tokenController;
    public Transform spawnpoint;

    private AsyncOperationHandle<GameObject> instanceHandle;
    void Start()
    {
        tokenController = new AcidToken();
        Debug.Log($"MODEL: {tokenController.data.tokenModelAddress}");
        Debug.Log($"MAT: {tokenController.data.tokenMaterialAddress}");
        instanceHandle = Addressables.InstantiateAsync(tokenController.data.tokenModelAddress, transform.position, transform.rotation);

        instanceHandle.Completed += op =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                GameObject instance = op.Result;
                var renderer = instance.GetComponent<Renderer>();

                Addressables.LoadAssetAsync<Material>("Prefabs/Tokens/AcidToken.mat")
    .Completed += handle =>
{
    if (handle.Status == UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
    {
        Material mat = handle.Result;
        instance.GetComponent<Renderer>().material = mat;
    }
    else
    {
        Debug.LogError("Failed to load material: " + tokenController.data.tokenMaterialAddress);
    }
};

                Debug.Log($"Spawned Addressable instance: {tokenController.data.tokenModelAddress}");
            }
            else
            {
                Debug.LogError($"Failed to instantiate Addressable: {tokenController.data.tokenModelAddress}");
            }
        };
    }
}