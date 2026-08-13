using Microsoft.EntityFrameworkCore;
using SuperBulider_AI.Data;
using SuperBulider_AI.Interfaces;
using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Services;


public class MetadataSearchService
	: IMetadataSearchService
{


	private readonly SuperBIContext _context;


	public MetadataSearchService(
		SuperBIContext context)
	{
		_context = context;
	}



	public async Task<List<MetadataSearchResult>> SearchAsync(
		string question,
		long tenantId)
	{


		var words =
			question
			.Split(
				new[]
				{
					' ',
					',',
					'，',
					'?',
					'？'
				},
				StringSplitOptions.RemoveEmptyEntries);



		var tables =
			await _context.MetadataTables

			.Include(x => x.Columns)

			.Where(x =>
				x.TenantId == tenantId)

			.ToListAsync();



		var result =
			new List<MetadataSearchResult>();



		foreach (var table in tables)
		{

			double score = 0;



			foreach (var word in words)
			{


				if (
					table.SearchText?
					.Contains(word)
					== true)
				{
					score += 1;
				}



				foreach (var column in table.Columns)
				{

					if (
						column.SearchText?
						.Contains(word)
						== true)
					{
						score += 2;
					}

				}

			}



			if (score > 0)
			{

				result.Add(
					new MetadataSearchResult
					{

						TableId =
							table.Id,


						TableName =
							table.TableName!,


						Columns =
							table.Columns
							.Select(x =>
								x.ColumnName!)
							.ToList(),


						Score = score

					});

			}

		}



		return result
			.OrderByDescending(x => x.Score)
			.Take(10)
			.ToList();

	}

}