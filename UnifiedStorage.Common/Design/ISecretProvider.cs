using System.Threading.Tasks;

namespace UnifiedStorage.Common.Design;

public interface ISecretProvider
{
    Task<string> GetSecretAsync(string secretName);
}