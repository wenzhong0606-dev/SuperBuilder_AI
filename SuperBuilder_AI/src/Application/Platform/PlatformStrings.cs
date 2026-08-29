namespace SuperBuilder_AI.Services.Platform;

/// <summary>
/// 平台内置本地化文案资源（P5 Multi-Language Runtime）。
///
/// <para>
/// 以静态字典承载而非 .resx：本平台是数据/BI 后端，面向终端用户的文案数量少且稳定，
/// 静态资源可保证取值<strong>纯确定性</strong>（无文件 IO、无区域性依赖、无程序集加载），
/// 便于在单元测试中离线断言，也避免为少量文案引入卫星程序集机制。
/// 业务语义的多语言标签走数据库（<c>SemanticLabel</c>），与本文件职责分离。
/// </para>
///
/// 扩展方式：为新语言在 <see cref="Resources"/> 登记一个以文化名为键的字典即可，
/// 解析侧通过 <see cref="ILocalizationService"/> 的回退链自动生效。
/// </summary>
public static class PlatformStrings
{
	/// <summary>
	/// 文案资源表：文化名 → (文案键 → 文案值)。
	/// 键使用 "域.项" 命名，与调用方传入的 key 保持一致。
	/// </summary>
	public static IReadOnlyDictionary<string, IReadOnlyDictionary<string, string>> Resources { get; } =
		new Dictionary<string, IReadOnlyDictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
		{
			["zh-CN"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Common.Unknown"] = "未知",
				["Common.NotApplicable"] = "不适用",
				["Query.EmptyQuestion"] = "问题不能为空。",
				["Query.NoSemanticMatch"] = "未找到与问题匹配的语义字段。",
				["Query.PlanFailed"] = "查询计划生成失败。",
				["Query.SqlBuildFailed"] = "SQL 生成失败。",
				["Query.ExecutionFailed"] = "查询执行失败。",
				["Semantic.NotFound"] = "未找到对应的字段语义。",
				["Entity.NotFound"] = "未找到对应的业务实体。",
				["Tenant.NotScoped"] = "当前请求未绑定租户作用域。",
				["Label.NotFound"] = "未找到对应语言的标签。",
			},
			["zh-TW"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Common.Unknown"] = "未知",
				["Common.NotApplicable"] = "不適用",
				["Query.EmptyQuestion"] = "問題不能為空。",
				["Query.NoSemanticMatch"] = "未找到與問題匹配的語義欄位。",
				["Query.PlanFailed"] = "查詢計劃產生失敗。",
				["Query.SqlBuildFailed"] = "SQL 產生失敗。",
				["Query.ExecutionFailed"] = "查詢執行失敗。",
				["Semantic.NotFound"] = "未找到對應的欄位語義。",
				["Entity.NotFound"] = "未找到對應的業務實體。",
				["Tenant.NotScoped"] = "當前請求未綁定租戶作用域。",
				["Label.NotFound"] = "未找到對應語言的標籤。",
			},
			["en-US"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Common.Unknown"] = "Unknown",
				["Common.NotApplicable"] = "N/A",
				["Query.EmptyQuestion"] = "Question cannot be empty.",
				["Query.NoSemanticMatch"] = "No semantic field matches the question.",
				["Query.PlanFailed"] = "Failed to build the query plan.",
				["Query.SqlBuildFailed"] = "Failed to generate SQL.",
				["Query.ExecutionFailed"] = "Query execution failed.",
				["Semantic.NotFound"] = "Field semantic not found.",
				["Entity.NotFound"] = "Business entity not found.",
				["Tenant.NotScoped"] = "The request is not bound to a tenant scope.",
				["Label.NotFound"] = "No label found for the requested language.",
			},
			["ja-JP"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Common.Unknown"] = "不明",
				["Common.NotApplicable"] = "該当なし",
				["Query.EmptyQuestion"] = "質問を入力してください。",
				["Query.NoSemanticMatch"] = "質問に一致する意味項目が見つかりません。",
				["Query.PlanFailed"] = "クエリプランの生成に失敗しました。",
				["Query.SqlBuildFailed"] = "SQL の生成に失敗しました。",
				["Query.ExecutionFailed"] = "クエリの実行に失敗しました。",
				["Semantic.NotFound"] = "項目の意味情報が見つかりません。",
				["Entity.NotFound"] = "業務エンティティが見つかりません。",
				["Tenant.NotScoped"] = "リクエストにテナントスコープが設定されていません。",
				["Label.NotFound"] = "指定言語のラベルが見つかりません。",
			},
			["ko-KR"] = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
			{
				["Common.Unknown"] = "알 수 없음",
				["Common.NotApplicable"] = "해당 없음",
				["Query.EmptyQuestion"] = "질문은 비워둘 수 없습니다.",
				["Query.NoSemanticMatch"] = "질문과 일치하는 시맨틱 필드를 찾지 못했습니다.",
				["Query.PlanFailed"] = "쿼리 계획 생성에 실패했습니다.",
				["Query.SqlBuildFailed"] = "SQL 생성에 실패했습니다.",
				["Query.ExecutionFailed"] = "쿼리 실행에 실패했습니다.",
				["Semantic.NotFound"] = "필드 시맨틱을 찾지 못했습니다.",
				["Entity.NotFound"] = "비즈니스 엔티티를 찾지 못했습니다.",
				["Tenant.NotScoped"] = "요청에 테넌트 범위가 지정되지 않았습니다.",
				["Label.NotFound"] = "요청한 언어의 라벨을 찾지 못했습니다.",
			},
		};

	/// <summary>
	/// 按回退链查找文案；全部未命中时返回 null（由调用方决定兜底策略）。
	/// </summary>
	public static string? Find(IReadOnlyList<string> fallbackChain, string key)
	{
		if (string.IsNullOrWhiteSpace(key)) return null;

		foreach (var culture in fallbackChain)
		{
			if (Resources.TryGetValue(culture, out var bundle)
				&& bundle.TryGetValue(key, out var value))
			{
				return value;
			}
		}

		return null;
	}
}
