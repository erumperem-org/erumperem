using UnityEngine;

public class PlayableAnimationController : MonoBehaviour
{
    private Animator _animator;

    private static readonly int IsMoving     = Animator.StringToHash("IsMoving");
    private static readonly int OpeningChest = Animator.StringToHash("OpeningChest");
    private static readonly int IsTalking    = Animator.StringToHash("IsTalking");
    private static readonly int OpeningDoor  = Animator.StringToHash("OpeningDoor");
    private static readonly int IsTorchOn    = Animator.StringToHash("IsTorchOn");
    private static readonly int ClosingDoor  = Animator.StringToHash("ClosingDoor");

    private void Awake() => _animator = GetComponent<Animator>();

    // ── Bools ─────────────────────────────────────────────────────────────
    public void SetIsMoving(bool value)  => _animator.SetBool(IsMoving, value);
    public void SetIsTalking(bool value) => _animator.SetBool(IsTalking, value);
    public void SetIsTorchOn(bool value) => _animator.SetBool(IsTorchOn, value);

    // ── Triggers ──────────────────────────────────────────────────────────
    public void TriggerOpeningChest() => _animator.SetTrigger(OpeningChest);
    public void TriggerOpeningDoor()  => _animator.SetTrigger(OpeningDoor);
    public void TriggerClosingDoor()  => _animator.SetTrigger(ClosingDoor);

    public void ResetOpeningChest() => _animator.ResetTrigger(OpeningChest);
    public void ResetOpeningDoor()  => _animator.ResetTrigger(OpeningDoor);
    public void ResetClosingDoor()  => _animator.ResetTrigger(ClosingDoor);
}
