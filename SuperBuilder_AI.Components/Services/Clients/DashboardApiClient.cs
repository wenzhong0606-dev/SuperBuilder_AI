using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Components.Services;

/// <summary>
/// 看板域客户端实现（M9-01）：看板/可视化数据的通用读原语，直接复用 <see cref="ApiClientBase"/> 提供的读方法。
/// 独立成客户端后，看板渲染逻辑可仅依赖 <see cref="IDashboardApiClient"/> 进行单元化测试，不牵连写/身份等其他域。
/// </summary>
public sealed class DashboardApiClient : ApiClientBase, IDashboardApiClient
{
    public DashboardApiClient(IHttpClientFactory factory, AppState appState) : base(factory, appState) { }
}
