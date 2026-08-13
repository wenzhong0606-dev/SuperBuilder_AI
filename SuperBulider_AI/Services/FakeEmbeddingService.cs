using System.Security.Cryptography;
using System.Text;
using SuperBulider_AI.Interfaces;


namespace SuperBulider_AI.Services;


/// <summary>
/// 测试Embedding服务
///
/// 当前用于验证:
///
/// Metadata
///      ↓
/// Vector
///      ↓
/// Qdrant
///
/// 后续替换:
///
/// QwenEmbeddingService
/// BgeEmbeddingService
///
/// </summary>
public class FakeEmbeddingService
	: IEmbeddingService
{


	/// <summary>
	/// 向量维度
	/// 必须和Qdrant Collection一致
	/// </summary>
	public int Dimension
		=> 1024;




	/// <summary>
	/// 当前模型名称
	/// 保存到Metadata
	/// </summary>
	public string ModelName
		=> "Fake-Embedding-1024";





	/// <summary>
	/// 文本生成向量
	/// </summary>
	public Task<float[]> GenerateAsync(
		string text)
	{


		if (string.IsNullOrWhiteSpace(text))
		{
			throw new ArgumentException(
				"Embedding文本不能为空",
				nameof(text));
		}



		/*
         * 使用固定Hash
         *
         * 保证同样文本
         * 每次生成相同向量
         */


		var bytes =
			SHA256.HashData(
				Encoding.UTF8
				.GetBytes(text));



		var seed =
			BitConverter
			.ToInt32(
				bytes,
				0);



		var random =
			new Random(seed);




		var vector =
			new float[Dimension];




		for (int i = 0; i < vector.Length; i++)
		{

			vector[i]
				=
				(float)
				random.NextDouble();

		}



		return Task.FromResult(vector);

	}

}