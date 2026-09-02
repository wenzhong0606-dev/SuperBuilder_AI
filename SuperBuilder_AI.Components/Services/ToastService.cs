namespace SuperBuilder_AI.Components.Services;

/// <summary>单条轻提示。</summary>
public sealed class ToastMessage
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    public string Text { get; set; } = "";
    /// <summary>语义色调：success / info / warning / danger。</summary>
    public string Tone { get; set; } = "info";
    public int TimeoutMs { get; set; } = 3200;
}

/// <summary>
/// 全局轻提示服务（Scoped）。页面通过注入调用 Show/Success/Error，
/// 渲染由 <c>SbToastHost</c> 在 MainLayout 统一承载。
/// </summary>
public sealed class ToastService
{
    private readonly List<ToastMessage> _items = new();

    public event Action? OnChanged;

    public IReadOnlyList<ToastMessage> Items => _items;

    public void Show(string text, string tone = "info", int timeoutMs = 3200)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _items.Add(new ToastMessage { Text = text, Tone = tone, TimeoutMs = timeoutMs });
        // 最多保留 5 条，避免长会话堆积
        while (_items.Count > 5) _items.RemoveAt(0);
        OnChanged?.Invoke();
    }

    public void Success(string text) => Show(text, "success");
    public void Info(string text) => Show(text, "info");
    public void Warning(string text) => Show(text, "warning", 4200);
    public void Error(string text) => Show(text, "danger", 5200);

    public void Dismiss(string id)
    {
        if (_items.RemoveAll(x => x.Id == id) > 0) OnChanged?.Invoke();
    }
}
