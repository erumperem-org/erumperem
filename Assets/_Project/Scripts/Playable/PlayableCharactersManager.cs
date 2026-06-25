using Player;
using Services.DebugUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Orquestra os personagens jogáveis.
///
/// ADIÇÃO em relação à versão anterior:
///   - <c>Playables</c>: expõe a lista gerenciada como <c>IReadOnlyList</c>
///     para que <see cref="ExplorationLoadContext"/> não precise de reflexão.
/// </summary>
[DefaultExecutionOrder(-100)]
public sealed class PlayableCharactersManager : MonoBehaviour
{
    [SerializeField] private List<PlayableCharacter> _playables;
    [SerializeField] private PlayerInputReader _inputReader;

    /// <summary>Lista somente-leitura de todos os personagens gerenciados.</summary>
    public IReadOnlyList<PlayableCharacter> Playables
    {
        get
        {
            EnsureSceneReferencesResolved();
            return _playables != null
                ? _playables
                : Array.Empty<PlayableCharacter>();
        }
    }

    public IPlayableCharacter Main { get; private set; }
    public IPlayableCharacter Companion { get; private set; }

    public event Action<IPlayableCharacter> OnMainChanged;
    public event Action<IPlayableCharacter> OnCompanionChanged;

    private readonly PlayableStateTransitioner _transitioner = new();

    /// <summary>
    /// Re-notifica ouvintes do Main actual (ex.: após load quando <see cref="SetState"/>
    /// não alterou o estado e <see cref="OnMainChanged"/> não foi invocado).
    /// </summary>
    public void NotifyCurrentMainIfAny()
    {
        if (Main != null)
        {
            OnMainChanged?.Invoke(Main);
        }
    }

    private void Awake()
    {
        EnsureSceneReferencesResolved();
    }

    private void EnsureSceneReferencesResolved()
    {
        if (_playables == null || _playables.Count == 0)
        {
            _playables = FindObjectsByType<PlayableCharacter>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .OrderBy(playableCharacter => playableCharacter.CharacterName, StringComparer.Ordinal)
                .ToList();
        }

        if (_inputReader == null)
        {
            _inputReader = FindFirstObjectByType<PlayerInputReader>();
        }

        RemoveNullPlayableEntries();
    }

    private void RemoveNullPlayableEntries()
    {
        if (_playables == null) return;

        for (int index = _playables.Count - 1; index >= 0; index--)
        {
            if (_playables[index] == null)
                _playables.RemoveAt(index);
        }
    }

    // ── API pública ───────────────────────────────────────────────────────

    public void SetState(PlayableCharacterState newState, PlayableCharacter character)
    {
        EnsureSceneReferencesResolved();

        if (!_playables.Contains(character))
            throw new InvalidOperationException(
                $"[PlayableCharactersManager] '{character.CharacterName}' não pertence à lista gerenciada.");

        if (newState == character.CurrentState) return;

        try
        {
            switch (newState)
            {
                case PlayableCharacterState.Main: PromoteToMain(character); break;
                case PlayableCharacterState.Companion: PromoteToCompanion(character); break;
                case PlayableCharacterState.Resting: PromoteToResting(character); break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(newState), newState, null);
            }

            LoggerService.PrintLogMessage(LogLevel.Debug,
                $"[{character.CharacterName.ToUpper()}] → {newState}.", LogCategory.Player);
        }
        catch (Exception e)
        {
            LoggerService.PrintLogMessage(LogLevel.Error,
                $"[{character.CharacterName.ToUpper()}] Falha ao transitar para {newState}: {e}",
                LogCategory.Player);
            throw;
        }
    }

    // ── Transições ────────────────────────────────────────────────────────

    private void PromoteToMain(PlayableCharacter next)
    {
        if (next == Companion)
        {
            var previousMain = Main as PlayableCharacter;
            if (previousMain != null)
            {
                _transitioner.ApplyCompanion(previousMain);
                previousMain.CurrentState = PlayableCharacterState.Companion;
                previousMain.UpdateStateExposed();
                Companion = previousMain;
                OnCompanionChanged?.Invoke(Companion);
            }
            else
            {
                Companion = null;
                OnCompanionChanged?.Invoke(null);
            }
        }
        else if (Main != null && Main != next)
        {
            PromoteToResting(Main as PlayableCharacter);
        }

        _transitioner.ApplyMain(next, _inputReader);
        next.CurrentState = PlayableCharacterState.Main;
        next.UpdateStateExposed();
        next.DetectionSystem.ClearAvailable();
        next.definition.battleFormationRank = 1;
        Main = next;
        OnMainChanged?.Invoke(Main);

        if (Companion != null && Companion != next)
            _transitioner.ApplyCompanion(Companion as PlayableCharacter);
    }

    private void PromoteToCompanion(PlayableCharacter next)
    {
        if (next == Main)
        {
            Main = null;
            OnMainChanged?.Invoke(null);
        }

        if (Companion != null && Companion != next)
            PromoteToResting(Companion as PlayableCharacter);

        _transitioner.ApplyCompanion(next);
        next.CurrentState = PlayableCharacterState.Companion;
        next.UpdateStateExposed();
        Companion = next;
        next.definition.battleFormationRank = 2;
        OnCompanionChanged?.Invoke(Companion);
    }

    private void PromoteToResting(PlayableCharacter character)
    {
        if (character == null) return;
        _transitioner.ApplyResting(character);
        character.CurrentState = PlayableCharacterState.Resting;
        character.UpdateStateExposed();
    }
}
