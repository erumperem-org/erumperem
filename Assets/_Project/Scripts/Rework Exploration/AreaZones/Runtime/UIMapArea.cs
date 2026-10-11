using UnityEngine;

/// <summary>
/// Área circular do mapa desenhado na UI (Canvas Overlay) - o par de
/// WorldMapArea do lado da UI. É essa referência, junto com uma CircularZone
/// de mundo, que PlayerMapIcon usa para converter posição de mundo em
/// posição de tela dentro do círculo do minimapa.
/// </summary>
public class UIMapArea : CircularUIZone
{
    protected override Color GizmoColor => Color.cyan;
}