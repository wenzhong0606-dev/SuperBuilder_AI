namespace SuperBuilder_AI.Models.AI;

/// <summary>
/// Qdrant向量搜索结果
///
/// 表示Qdrant返回的相似Metadata
/// </summary>
public class VectorSearchResult
{


    /// <summary>
    /// Qdrant Point Id
    /// </summary>
    public string Id { get; set; }
        = string.Empty;



    /// <summary>
    /// 相似度评分
    ///
    /// Cosine:
    /// 越接近1越相似
    /// </summary>
    public double Score { get; set; }



    /// <summary>
    /// Payload数据
    ///
    /// 保存Metadata业务信息
    /// </summary>
    public Dictionary<string, object> Payload { get; set; }
        = new();

}