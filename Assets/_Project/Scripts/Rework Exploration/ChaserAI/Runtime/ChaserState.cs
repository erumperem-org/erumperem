/// <summary>
/// Estados possíveis da IA perseguidora (<see cref="ChaserAI"/>).
///
/// Não existe mais um estado "Resting": todo ChaserAI da cena fica sempre
/// ativo, alternando apenas entre os três estados abaixo. O
/// <c>ChaserPool</c> ainda pode teleportar um Chaser para perto do player
/// quando ele se afasta demais (ver <see cref="ChaserAI.Relocate"/>), mas
/// isso nunca desliga movimento/percepção - o Chaser simplesmente reaparece
/// em Wandering.
/// </summary>
public enum ChaserState
{
    /// <summary>Trafega em pontos aleatórios dentro da área patrulhável, procurando o alvo.</summary>
    Wandering,

    /// <summary>Persegue o alvo diretamente enquanto ele estiver perceptível.</summary>
    Chasing,

    /// <summary>Perdeu o alvo (saiu do raio de percepção ou teve a linha de visão bloqueada);
    /// vai até a última posição conhecida e aguarda antes de desistir.</summary>
    Investigating
}
