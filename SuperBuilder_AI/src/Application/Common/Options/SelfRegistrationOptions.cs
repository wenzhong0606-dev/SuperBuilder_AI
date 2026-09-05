namespace SuperBuilder_AI.Application.Common.Options;

/// <summary>
/// 租户自助注册配置（M2-06 骨架，默认关闭）。
/// <para>
/// 设计原则（DEC-04）：默认关闭，由平台按环境显式开启；涉及公网开放的子特性
/// （审批、验证码、防滥用）尚未裁决，一旦开启即拒绝注册，避免不安全地对外开放。
/// </para>
/// </summary>
public sealed class SelfRegistrationOptions
{
    public const string SectionName = "SelfRegistration";

    /// <summary>是否开放公网自助注册。默认 false（DEC-04 推荐默认：默认关闭，平台按环境开启）。</summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// 是否需要平台管理员审批后才启用租户。M2-06 待裁决：审批工作流尚未实现，
    /// 开启时注册将被拒绝（避免开放一个无人审批、租户立即可用的通道）。
    /// </summary>
    public bool ApprovalRequired { get; set; }

    /// <summary>
    /// 是否需要验证码（防滥用）。M2-06 待裁决：验证码提供方尚未集成，
    /// 开启时注册将被拒绝。
    /// </summary>
    public bool RequireCaptcha { get; set; }

    /// <summary>允许注册的管理员邮箱域名白名单；null/空表示不限制。</summary>
    public string[]? AllowedEmailDomains { get; set; }

    /// <summary>新建租户默认语言。</summary>
    public string DefaultCulture { get; set; } = "zh-CN";

    /// <summary>新建租户可用语言列表。</summary>
    public string[] DefaultAvailableCultures { get; set; } = new[] { "zh-CN" };

    /// <summary>新建租户默认配额策略代码（可选，M2-07 配额策略落地后联动）。</summary>
    public string? DefaultQuotaPolicyCode { get; set; }
}
