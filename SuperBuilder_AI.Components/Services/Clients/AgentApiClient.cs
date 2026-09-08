using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// Agent 域客户端实现（M9-01）：Agent/App 构建器所需的通用写原语，直接复用 <see cref="ApiClientBase"/> 提供的写方法。
/// 独立成客户端后，Agent 构建逻辑可仅依赖 <see cref="IAgentApiClient"/> 进行单元化测试，不牵连读/身份等其他域。
/// </summary>
public sealed class AgentApiClient : ApiClientBase, IAgentApiClient
{
    public AgentApiClient(IHttpClientFactory factory, AppState appState) : base(factory, appState) { }
}
