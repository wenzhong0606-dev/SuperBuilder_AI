using Microsoft.Extensions.Options;
using Qdrant.Client;
using Qdrant.Client.Grpc;
using SuperBulider_AI.Configuration;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.AI;


namespace SuperBulider_AI.Services;

/// <summary>
/// Qdrant实现
/// </summary>
public class QdrantService
	: IQdrantService
{


	private readonly QdrantClient _client;


	private readonly QdrantOptions _options;



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
	/// 判断Collection
	/// </summary>
	public async Task<bool> ExistsAsync()
	{

		return await _client
			.CollectionExistsAsync(
				_options.CollectionName);

	}





	/// <summary>
	/// 创建Collection
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
	/// 插入Vector
	/// </summary>
	public async Task UpsertAsync(
		string id,
		float[] vector,
		Dictionary<string, object> payload)
	{

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

				ConvertPayload(item.Value)

			);

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
	/// Query搜索
	/// </summary>
	public async Task<List<VectorSearchResult>> QueryAsync(
		float[] vector,
		int limit = 10)
	{


		var result =
			await _client
			.QueryAsync(

				collectionName:
					_options.CollectionName,


				query:
					vector,


				limit:
					(ulong)limit

			);



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
							ConvertPayloadValue(x.Value))

				})
			.ToList();

	}


	private object ConvertPayloadValue(Value value)
	{

		switch (value.KindCase)
		{

			case Value.KindOneofCase.StringValue:

				return value.StringValue;



			case Value.KindOneofCase.IntegerValue:

				return value.IntegerValue;



			case Value.KindOneofCase.DoubleValue:

				return value.DoubleValue;



			case Value.KindOneofCase.BoolValue:

				return value.BoolValue;



			default:

				return string.Empty;

		}

	}



	/// <summary>
	/// 删除
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
						Uuid=id
					}
				});

	}






	private Value ConvertPayload(
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


			_ =>
				new Value
				{
					StringValue =
					value.ToString()
				}

		};

	}

}