using System;
using UnityEngine;

/// <summary>
/// Registro individual de um personagem salvo: id, estado (como texto livre
/// - este pacote não conhece o enum CharacterState de PlayableCharacters),
/// posição e rotação. Vector3/Quaternion são suportados nativamente pelo
/// JsonUtility, então nenhum wrapper extra é necessário.
/// </summary>
[Serializable]
public class CharacterSaveRecord
{
    public string Id;
    public string State;
    public Vector3 Position;
    public Quaternion Rotation;
}
