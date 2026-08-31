using Microsoft.JSInterop;
using System.Threading.Tasks;

namespace SuperBuilder_AI.Components.Services;

/// <summary>运行时主题骨架：将主题名写入根元素 data-theme，CSS 变量随之切换（P11.2 接入 api/themes 拉取设计令牌）。</summary>
public sealed class ThemeService
{
    private readonly IJSRuntime _js;
    public string CurrentTheme { get; private set; } = "light";

    public ThemeService(IJSRuntime js) => _js = js;

    public async Task ApplyAsync(string theme)
    {
        CurrentTheme = theme;
        await _js.InvokeVoidAsync("SuperBuilder.setTheme", theme);
    }
}
