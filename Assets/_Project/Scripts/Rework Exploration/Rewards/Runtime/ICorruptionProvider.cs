using System.Threading.Tasks;

namespace Core.Rewards
{
    /// <summary>Supplies the corruption value used to pick the reward tier.</summary>
    public interface ICorruptionProvider
    {
        Task<double> GetCorruptionAsync();
    }
}
