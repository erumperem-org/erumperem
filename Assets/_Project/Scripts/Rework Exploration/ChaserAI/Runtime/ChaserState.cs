/// <summary>
/// Estados possíveis da IA perseguidora (<see cref="ChaserAI"/>).
/// </summary>
public enum ChaserState
{
    /// <summary>Trafega em pontos aleatórios dentro da área patrulhável, procurando o alvo.</summary>
    Wandering,

    /// <summary>Persegue o alvo diretamente enquanto ele estiver perceptível.</summary>
    Chasing,

    /// <summary>Perdeu o alvo (saiu do raio de percepção ou teve a linha de visão bloqueada);
    /// vai até a última posição conhecida e aguarda antes de desistir.</summary>
    Investigating,

    /// <summary>Nenhuma rotina de movimento/percepção ativa. Só é ligado/desligado
    /// por evento externo (ex: Hub do pacote AreaZones).</summary>
    Resting
}
