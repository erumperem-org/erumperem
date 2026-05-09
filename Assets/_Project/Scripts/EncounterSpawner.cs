using UnityEngine;

public class RandomPrefabSpawner : MonoBehaviour
{
	[Header("Prefabs to Spawn")]
	public GameObject[] prefabs;

	[Header("Spawn Targets")]
	public Transform[] targetPoints;

	private void Start()
	{
		SpawnRandomPrefabs();
	}

	void SpawnRandomPrefabs()
	{
		if (prefabs.Length == 0 || targetPoints.Length == 0)
		{
			Debug.LogWarning("Missing prefabs or target points.");
			return;
		}

		foreach (Transform target in targetPoints)
		{
			if (target == null)
				continue;

			int randomIndex = Random.Range(0, prefabs.Length);

			GameObject selectedPrefab = prefabs[randomIndex];

			Instantiate(
				selectedPrefab,
				target.position,
				target.rotation,
				target
			);
		}
	}
}