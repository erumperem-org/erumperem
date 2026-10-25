using System;

namespace Core.Shop
{
    /// <summary>
    /// Ponto de disparo do evento que avança os botões rotativos para a próxima oferta.
    /// channel vazio/null = avança TODOS os RotatingItemShopButton.
    /// </summary>
    public static class RotatingShopEvents
    {
        public static event Action<string> OnAdvance;

        public static void RaiseAdvance(string channel = null) => OnAdvance?.Invoke(channel);
    }
}
