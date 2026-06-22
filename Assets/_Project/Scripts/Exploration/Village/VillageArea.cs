using System.Collections;
using UnityEngine;

/// <summary>
/// Zona trigger da vila. Emite eventos quando o personagem Main entra ou sai.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(SphereCollider))]
public sealed class VillageArea : MonoBehaviour
{
    [SerializeField] private PlayableCharactersManager _playableCharactersManager;

    private SphereCollider _sphereCollider;
    private bool _isPlayerInside;

    public bool IsPlayerInside => _isPlayerInside;

    private void Awake()
    {
        _sphereCollider = GetComponent<SphereCollider>();
        _sphereCollider.isTrigger = true;

        if (_playableCharactersManager == null)
        {
            _playableCharactersManager = FindFirstObjectByType<PlayableCharactersManager>();
        }
    }

    private void Start()
    {
        StartCoroutine(ApplySanctuaryIfMainAlreadyInsideAfterLoad());
    }

    private void OnTriggerEnter(Collider otherCollider)
    {
        if (_isPlayerInside || !IsMainCharacterCollider(otherCollider))
        {
            return;
        }

        _isPlayerInside = true;
        ExplorationVillageEvents.RaisePlayerEnteredVillage();
    }

    private void OnTriggerExit(Collider otherCollider)
    {
        if (!_isPlayerInside || !IsMainCharacterCollider(otherCollider))
        {
            return;
        }

        _isPlayerInside = false;
        ExplorationVillageEvents.RaisePlayerExitedVillage();
    }

    private IEnumerator ApplySanctuaryIfMainAlreadyInsideAfterLoad()
    {
        yield return null;

        if (_isPlayerInside)
        {
            yield break;
        }

        if (!IsMainCharacterInsideSanctuarySphere())
        {
            yield break;
        }

        _isPlayerInside = true;
        ExplorationVillageEvents.RaisePlayerEnteredVillage();
    }

    private bool IsMainCharacterInsideSanctuarySphere()
    {
        if (_playableCharactersManager?.Main?.Transform == null || _sphereCollider == null)
        {
            return false;
        }

        var sanctuaryCenter = transform.TransformPoint(_sphereCollider.center);
        var lossyScale = transform.lossyScale;
        var worldRadius = _sphereCollider.radius * Mathf.Max(lossyScale.x, lossyScale.y, lossyScale.z);
        var mainPosition = _playableCharactersManager.Main.Transform.position;
        return (mainPosition - sanctuaryCenter).sqrMagnitude <= worldRadius * worldRadius;
    }

    private bool IsMainCharacterCollider(Collider otherCollider)
    {
        if (otherCollider == null || _playableCharactersManager?.Main == null)
        {
            return false;
        }

        var mainPlayableCharacter = _playableCharactersManager.Main as PlayableCharacter;
        if (mainPlayableCharacter != null)
        {
            var playableOnCollider = otherCollider.GetComponentInParent<PlayableCharacter>();
            if (playableOnCollider != null)
            {
                return ReferenceEquals(playableOnCollider, mainPlayableCharacter);
            }
        }

        if (!otherCollider.CompareTag("Player"))
        {
            return false;
        }

        var mainTransform = _playableCharactersManager.Main.Transform;
        return mainTransform != null &&
               (otherCollider.transform == mainTransform ||
                otherCollider.transform.IsChildOf(mainTransform));
    }
}
