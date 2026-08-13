using System.Security.Cryptography;
using System.Text;
using SuperBulider_AI.Interfaces;

namespace SuperBulider_AI.Services;

/// <summary>
/// 测试 Embedding 服务。
///
/// 当前用于开发环境验证:
///
/// Metadata
///      ↓
/// Vector
///      ↓
/// Qdrant
///
/// 注意:
///
/// FakeEmbedding 不具备真实语义能力。
///
/// 它只是保证:
///
/// 相同文本
///      ↓
/// 相同向量
///
/// 用于测试整个向量链路。
///
/// 正式环境使用:
///
/// QwenEmbeddingService
/// </summary>
public class FakeEmbeddingService
	: IEmbeddingService
{
	/// <summary>
	/// 向量维度。
	///
	/// 必须与 Qdrant Collection 保持一致。
	/// </summary>
	public int Dimension
		=> 1024;

	/// <summary>
	/// 当前模型名称。
	/// </summary>
	public string ModelName
		=> "Fake-Embedding-1024";

	/// <summary>
	/// 文本生成向量。
	/// </summary>
	/// <param name="text">
	/// 待生成向量的文本。
	/// </param>
	/// <param name="textType">
	/// 文本类型。
	///
	/// FakeEmbedding 当前不区分 query/document，
	/// 参数仅为了与真实 Embedding 接口保持一致。
	/// </param>
	public Task<float[]> GenerateAsync(
		string text,
		string textType = "document")
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ArgumentException(
				"Embedding文本不能为空。",
				nameof(text));
		}

		/*
         * 使用固定 Hash。
         *
         * 保证相同文本:
         *
         * Text
         *   ↓
         * SHA256
         *   ↓
         * Random Seed
         *   ↓
         * 相同 Vector
         */
		var bytes =
			SHA256.HashData(
				Encoding.UTF8.GetBytes(text));

		var seed =
			BitConverter.ToInt32(
				bytes,
				0);

		var random =
			new Random(seed);

		var vector =
			new float[Dimension];

		for (var i = 0;
			 i < vector.Length;
			 i++)
		{
			vector[i] =
				(float)random.NextDouble();
		}

		return Task.FromResult(vector);
	}
}