using UnityEditor.Animations;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class MovimentoXZ : MonoBehaviour
{
	public float velocidade = 5f;
	public float velocidadeRotacao = 10f;

	private Rigidbody rb;
	private Vector3 movimento;
	public Animator wulfricAnimatorController;

	void Start()
	{
		rb = GetComponent<Rigidbody>();
	}

	private void OnEnable()
	{
		if (InputManager.Instance != null)
		{
			InputManager.Instance.OnMoveChanged += HandleMoveChanged;
		}
	}

	private void OnDisable()
	{
		if (InputManager.Instance != null)
		{
			InputManager.Instance.OnMoveChanged -= HandleMoveChanged;
		}

		movimento = Vector3.zero;
	}

	private void HandleMoveChanged(Vector2 movementInput)
	{
		movimento = new Vector3(movementInput.x, 0f, movementInput.y).normalized;
        wulfricAnimatorController.SetFloat("Speed", movementInput.x + movementInput.y);

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