using System.Collections.Generic;

namespace Erumperem.Progression
{
    /// <summary>JSON shape for <see cref="PlayerProgressionService"/> persistence.</summary>
    public sealed class PlayerProgressionSaveDto
    {
        public int Version { get; set; } = 2;

        /// <summary>
        /// Current shared skill level (0..max). Each party member in the shared-level group
        /// may spend up to this many points in their own tree independently.
        /// </summary>
        public int SharedSkillLevel { get; set; }

        /// <summary>Character id (e.g. wulfric) → node id → unlocked.</summary>
        public Dictionary<string, Dictionary<string, bool>> Characters { get; set; } = new();
    }
}
