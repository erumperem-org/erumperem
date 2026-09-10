using System;

/// <summary>
/// Um registro de vitalidade por personagem. Guarda a porcentagem
/// (normalizada, 0-1) da vida no momento do save, não o valor absoluto -
/// porque o Max efetivo pode mudar entre sessões (base stats + modificador
/// de item), então reaplicar a mesma porcentagem sobre o novo Max é o
/// comportamento correto, diferente de reaplicar um valor absoluto antigo.
/// </summary>
[Serializable]
public class CharacterVitalsRecord
{
    public string CharacterId;
    public float HealthNormalized;
}