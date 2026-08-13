using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SuperBulider_AI.Configuration;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;

namespace SuperBulider_AI.Services;

/// <summary>
/// Qdrant 向量数据库实现。
/// </summary>
public class QdrantService
	: IQdrantService
{
	private readonly QdrantClient _client;
	private readonly QdrantOptions _options;

	/// <summary>
	/// 创建 Qdrant 服务。
	/// </summary>
	public QdrantService(
		IOptions<QdrantOptions> options)
	{
		_options =
			options.Value;

		_client =
			new QdrantClient(
				host: _options.Host,
				port: _options.Port);
	}

	/// <summary>
	/// 判断 Collection 是否存在。
	/// </summary>
	public async Task<bool> ExistsAsync()
	{
		return await _client
			.CollectionExistsAsync(
				_options.CollectionName);
	}

	/// <summary>
	/// 创建 Collection。
	///
	/// 已存在时直接返回。
	/// </summary>
	public async Task CreateCollectionAsync()
	{
		if (await ExistsAsync())
		{
			return;
		}

		await _client
			.CreateCollectionAsync(
				collectionName:
					_options.CollectionName,

				vectorsConfig:
					new VectorParams
					{
						Size =
							_options.VectorSize,

						Distance =
							Distance.Cosine
					});
	}

	/// <summary>
	/// 删除旧 Collection，
	/// 然后按照当前配置重新创建。
	///
	/// 当前项目:
	///
	/// VectorSize = 1024
	/// Distance = Cosine
	/// </summary>
	public async Task RecreateCollectionAsync()
	{
		if (await ExistsAsync())
		{
			await _client
				.DeleteCollectionAsync(
					_options.CollectionName);
		}

		await _client
			.CreateCollectionAsync(
				collectionName:
					_options.CollectionName,

				vectorsConfig:
					new VectorParams
					{
						Size =
							_options.VectorSize,

						Distance =
							Distance.Cosine
					});
	}

	/// <summary>
	/// 插入或更新 Vector。
	/// </summary>
	public async Task UpsertAsync(
		string id,
		float[] vector,
		Dictionary<string, object> payload)
	{
		if (vector == null ||
			vector.Length == 0)
		{
			throw new ArgumentException(
				"Vector不能为空。",
				nameof(vector));
		}

		if ((ulong)vector.Length !=
			_options.VectorSize)
		{
			throw new InvalidOperationException(
				$"Vector维度不匹配。" +
				$"Qdrant配置={_options.VectorSize}。" +
				$"实际={vector.Length}。");
		}

		await CreateCollectionAsync();

		var point =
			new PointStruct
			{
				Id =
					new PointId
					{
						Uuid = id
					},

				Vectors =
					vector
			};

		foreach (var item in payload)
		{
			point.Payload.Add(
				item.Key,
				ConvertPayload(item.Value));
		}

		await _client
			.UpsertAsync(
				collectionName:
					_options.CollectionName,

				points:
					new[]
					{
						point
					});
	}

	/// <summary>
	/// 搜索 Vector。
	/// </summary>
	public async Task<List<VectorSearchResult>> QueryAsync(
		float[] vector,
		int limit = 10)
	{
		if (vector == null ||
			vector.Length == 0)
		{
			throw new ArgumentException(
				"Query Vector不能为空。",
				nameof(vector));
		}

		if ((ulong)vector.Length !=
			_options.VectorSize)
		{
			throw new InvalidOperationException(
				$"Query Vector维度不匹配。" +
				$"Qdrant配置={_options.VectorSize}。" +
				$"实际={vector.Length}。");
		}

		await CreateCollectionAsync();

		var result =
			await _client.QueryAsync(
				collectionName:
					_options.CollectionName,

				query:
					vector,

				limit:
					(ulong)limit);

		return result
			.Select(x =>
				new VectorSearchResult
				{
					Id =
						x.Id.Uuid,

					Score =
						x.Score,

					Payload =
						x.Payload
							.ToDictionary(
								x => x.Key,
								x =>
									ConvertPayloadValue(
										x.Value))
				})
			.ToList();
	}

	/// <summary>
	/// 删除指定 Vector。
	/// </summary>
	public async Task DeleteAsync(
		string id)
	{
		await _client
			.DeleteAsync(
				_options.CollectionName,

				new[]
				{
					new PointId
					{
						Uuid = id
					}
				});
	}

	/// <summary>
	/// 将普通对象转换成 Qdrant Payload Value。
	/// </summary>
	private static Value ConvertPayload(
		object value)
	{
		return value switch
		{
			string s =>
				new Value
				{
					StringValue = s
				},

			int i =>
				new Value
				{
					IntegerValue = i
				},

			long l =>
				new Value
				{
					IntegerValue = l
				},

			bool b =>
				new Value
				{
					BoolValue = b
				},

			double d =>
				new Value
				{
					DoubleValue = d
				},

			float f =>
				new Value
				{
					DoubleValue = f
				},

			decimal m =>
				new Value
				{
					DoubleValue =
						(double)m
				},

			_ =>
				new Value
				{
					StringValue =
						value.ToString()
						?? string.Empty
				}
		};
	}

	/// <summary>
	/// 将 Qdrant Payload Value 转换为普通对象。
	/// </summary>
	private static object ConvertPayloadValue(
		Value value)
	{
		return value.KindCase switch
		{
			Value.KindOneofCase.StringValue =>
				value.StringValue,

			Value.KindOneofCase.IntegerValue =>
				value.IntegerValue,

			Value.KindOneofCase.DoubleValue =>
				value.DoubleValue,

			Value.KindOneofCase.BoolValue =>
				value.BoolValue,

			_ =>
				string.Empty
		};
	}
}