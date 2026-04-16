using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class MovimentoXZ : MonoBehaviour
{
	public float velocidade = 5f;
	public float velocidadeRotacao = 10f;

	private Rigidbody rb;
	private Vector3 movimento;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
	}

	void Update()
	{
		Vector2 input = Keyboard.current != null ? new Vector2(
			(Keyboard.current.dKey.isPressed ? 1 : 0) - (Keyboard.current.aKey.isPressed ? 1 : 0),
			(Keyboard.current.wKey.isPressed ? 1 : 0) - (Keyboard.current.sKey.isPressed ? 1 : 0)
		) : Vector2.zero;

		movimento = new Vector3(input.x, 0f, input.y).normalized;
	}

	void FixedUpdate()
	{
		rb.MovePosition(rb.position + movimento * velocidade * Time.fixedDeltaTime);

		if (movimento != Vector3.zero)
		{
			Quaternion rotacaoDesejada = Quaternion.LookRotation(movimento);
			Quaternion rotacaoSuave = Quaternion.Slerp(rb.rotation, rotacaoDesejada, velocidadeRotacao * Time.fixedDeltaTime);
			rb.MoveRotation(rotacaoSuave);
		}
	}
}