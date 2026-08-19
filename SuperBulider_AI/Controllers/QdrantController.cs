using Microsoft.AspNetCore.Mvc;
using SuperBuilder_AI.Interfaces;


namespace SuperBuilder_AI.Controllers;


public class QdrantController
	: Controller
{


	private readonly IQdrantService _service;



	public QdrantController(
		IQdrantService service)
	{
		_service = service;
	}



	public async Task<IActionResult> Test()
	{


		var vector =
			new float[1024];


		vector[0] = 1;



		var id =
			Guid.NewGuid()
			.ToString();



		await _service
			.UpsertAsync(

				id,

				vector,

				new Dictionary<string, object>
				{

					["table"] = "SalesOrder",

					["column"] = "NetAmount",

					["description"] = "销售净金额"

				});



		var result =
			await _service
			.QueryAsync(
				vector);



		return Json(result);

	}

}