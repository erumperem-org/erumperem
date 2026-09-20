using System.Collections.Generic;
using Player;
using UnityEngine;

public sealed class UiGameplayInputBlocker
{
    private readonly Dictionary<PlayerInputReader, bool> _previousBlockedStates = new();
    private bool _isBlocking;

    public void SetBlocked(bool blocked)
    {
        if (blocked)
        {
            Block();
        }
        else
        {
            Release();
        }
    }

    public void Refresh()
    {
        if (!_isBlocking)
        {
            return;
        }

        var inputReaders = Object.FindObjectsByType<PlayerInputReader>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (var readerIndex = 0; readerIndex < inputReaders.Length; readerIndex++)
        {
            var inputReader = inputReaders[readerIndex];

            if (inputReader == null)
            {
                continue;
            }

            if (!_previousBlockedStates.ContainsKey(inputReader))
            {
                _previousBlockedStates[inputReader] = inputReader.IsBlocked;
            }

            inputReader.IsBlocked = true;
        }
    }

    public void Release()
    {
        if (!_isBlocking)
        {
            return;
        }

        foreach (var blockedState in _previousBlockedStates)
        {
            var inputReader = blockedState.Key;

            if (inputReader == null)
            {
                continue;
            }

            inputReader.IsBlocked = blockedState.Value;
        }

        _previousBlockedStates.Clear();
        _isBlocking = false;
    }

    private void Block()
    {
        if (!_isBlocking)
        {
            _isBlocking = true;
        }

        Refresh();
    }
}
