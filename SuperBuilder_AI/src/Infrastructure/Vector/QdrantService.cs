using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using Grpc.Net.Client;
using System.Net.Http;
using SuperBuilder_AI.Application.Common.Options;
using SuperBuilder_AI.Interfaces;
using SuperBuilder_AI.Models.AI;
using System.Collections.Generic;
using System.Threading;

namespace SuperBuilder_AI.Services;

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

		// 使用自定义 HttpClient 连接 Qdrant：显式绕过系统代理。
		// 若宿主环境设置了 HTTP_PROXY/HTTPS_PROXY（常见于开发机/CI），SocketsHttpHandler
		// 会默认把 localhost 的 h2c gRPC 请求也发往代理，代理不支持明文 HTTP/2，
		// 导致 "unable to establish HTTP/2 connection"。Qdrant 是本地/内网服务，
		// 应始终直连，不走代理。
		var handler = new SocketsHttpHandler
		{
			EnableMultipleHttp2Connections = true,
			UseProxy = false,
		};

		_client =
			new QdrantClient(
				new QdrantGrpcClient(
					GrpcChannel.ForAddress(
						$"http://{_options.Host}:{_options.Port}",
						new GrpcChannelOptions { HttpHandler = handler })));
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
		Dictionary<string, object> payload,
		CancellationToken ct = default)
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
					},
				cancellationToken: ct);
	}

	/// <summary>
	/// 批量插入或更新 Vector。单次 gRPC 调用写入全部 points。
	/// 维度校验与单条一致；空 points 直接返回。
	/// </summary>
	public async Task UpsertBatchAsync(
		IEnumerable<(string Id, float[] VectorData, IReadOnlyDictionary<string, object> Payload)> points,
		CancellationToken ct = default)
	{
		var list = points?.ToList()
			?? new List<(string, float[], IReadOnlyDictionary<string, object>)>();
		if (list.Count == 0)
			return;

		foreach (var (id, vector, payload) in list)
		{
			if (vector == null ||
				vector.Length == 0)
			{
				throw new ArgumentException(
					"Vector不能为空。",
					nameof(points));
			}

			if ((ulong)vector.Length !=
				_options.VectorSize)
			{
				throw new InvalidOperationException(
					$"Vector维度不匹配。" +
					$"Qdrant配置={_options.VectorSize}。" +
					$"实际={vector.Length}。");
			}
		}

		await CreateCollectionAsync();

		var structs = list.Select(p =>
		{
			var point = new PointStruct
			{
				Id = new PointId
				{
					Uuid = p.Id
				},

				Vectors = p.VectorData
			};

			foreach (var item in p.Payload)
			{
				point.Payload.Add(
					item.Key,
					ConvertPayload(item.Value));
			}

			return point;
		}).ToArray();

		await _client
			.UpsertAsync(
				collectionName:
					_options.CollectionName,

				points: structs,
				cancellationToken: ct);
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
	/// 列出 Collection 中全部 Vector Point ID（用于孤儿检测）。
	/// 通过滚动游标分页获取，避免一次性加载向量本身。
	/// </summary>
	public async Task<IReadOnlyList<string>> ListPointIdsAsync(
		CancellationToken cancellationToken = default)
	{
		if (!await ExistsAsync())
		{
			return Array.Empty<string>();
		}

		var ids = new List<string>();
		PointId? offset = null;

		while (true)
		{
			var response = await _client.ScrollAsync(
				collectionName: _options.CollectionName,
				limit: 256,
				offset: offset,
				cancellationToken: cancellationToken);

			var count = 0;
			foreach (var point in response.Result)
			{
				ids.Add(point.Id.Uuid);
				offset = point.Id;
				count++;
			}

			// 当返回数量不足一页时，说明已到达末尾。
			if (count < 256)
			{
				break;
			}
		}

		return ids;
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