using System.Collections.Generic;

namespace Erumperem.Progression
{
    /// <summary>JSON shape for <see cref="PlayerProgressionService"/> persistence.</summary>
    public sealed class PlayerProgressionSaveDto
    {
        public int Version { get; set; } = 1;

        /// <summary>Character id (e.g. wulfric) → node id → unlocked.</summary>
        public Dictionary<string, Dictionary<string, bool>> Characters { get; set; } = new();
    }
}
