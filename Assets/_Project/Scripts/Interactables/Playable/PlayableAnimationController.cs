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

    private void Awake()
    {
        _animator = GetComponent<Animator>();
        if (_animator == null)
        {
            _animator = GetComponentInChildren<Animator>();
        }
    }

    // ── Bools ─────────────────────────────────────────────────────────────
    public void SetIsMoving(bool value)
    {
        if (_animator == null) return;
        _animator.SetBool(IsMoving, value);
    }

    public void SetIsTalking(bool value)
    {
        if (_animator == null) return;
        _animator.SetBool(IsTalking, value);
    }

    public void SetIsTorchOn(bool value)
    {
        if (_animator == null) return;
        _animator.SetBool(IsTorchOn, value);
    }

    // ── Triggers ──────────────────────────────────────────────────────────
    public void TriggerOpeningChest()
    {
        if (_animator == null) return;
        _animator.SetTrigger(OpeningChest);
    }

    public void TriggerOpeningDoor()
    {
        if (_animator == null) return;
        _animator.SetTrigger(OpeningDoor);
    }

    public void TriggerClosingDoor()
    {
        if (_animator == null) return;
        _animator.SetTrigger(ClosingDoor);
    }

    public void ResetOpeningChest()
    {
        if (_animator == null) return;
        _animator.ResetTrigger(OpeningChest);
    }

    public void ResetOpeningDoor()
    {
        if (_animator == null) return;
        _animator.ResetTrigger(OpeningDoor);
    }

    public void ResetClosingDoor()
    {
        if (_animator == null) return;
        _animator.ResetTrigger(ClosingDoor);
    }
}
