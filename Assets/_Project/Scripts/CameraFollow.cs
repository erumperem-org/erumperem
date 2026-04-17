using UnityEngine;

public class CameraFollow : MonoBehaviour
{
	public Transform alvo;
	public Vector3 offset = new Vector3(0f, 5f, -7f);
	public float suavidade = 5f;

	void LateUpdate()
	{
		if (alvo == null) return;

		Vector3 posicaoDesejada = alvo.position + offset;

		transform.position = Vector3.Lerp(
			transform.position,
			posicaoDesejada,
			suavidade * Time.deltaTime
		);
	}
}