using UnityEngine;

public class TreeColilision : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
		GetComponent<TerrainCollider>().enabled = false;
		GetComponent<TerrainCollider>().enabled = true;
	}

}
