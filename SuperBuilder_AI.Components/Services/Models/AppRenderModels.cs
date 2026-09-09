using System.Collections.Generic;

namespace SuperBuilder_AI.Components.Services.Models;

/// <summary>M7-11：应用运行/预览响应（客户端模型，对齐后端 <c>AppRenderModel</c>）。</summary>
public sealed record AppRenderDto(
    string Code,
    string Name,
    string? ThemeKey,
    int? PublishedVersion,
    bool Succeeded,
    List<AppComponentRenderDto> Components);

/// <summary>M7-11：单个组件渲染结果（客户端模型）。</summary>
public sealed record AppComponentRenderDto(
    string Id,
    string Type,
    string? Title,
    string? ChartType,
    List<string> AxisFields,
    List<AppSeriesSpecDto> Series,
    string? Text,
    List<AppColumnSpecDto> Columns,
    List<Dictionary<string, object?>> Data,
    bool Succeeded,
    string? ErrorCode,
    string? ErrorMessage);

/// <summary>结果列元数据。</summary>
public sealed record AppColumnSpecDto(string Name, string? DisplayName, string? Type);

/// <summary>图表度量序列。</summary>
public sealed record AppSeriesSpecDto(string Name, string? DisplayName);

/// <summary>M7-11：渲染调用结果（统一成功/失败/状态码）。</summary>
public sealed record AppRenderResult(
    bool Ok,
    AppRenderDto? Model,
    int Status,
    string? ErrorMessage,
    string? ErrorCode);
