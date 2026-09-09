using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace SuperBuilder_AI.Api.Security;

/// <summary>
/// Removes the Development/Test diagnostic &amp; fixture controllers from endpoint
/// discovery in every non-Development environment (M9-04 / SB-P0-09 诊断分区).
/// Covers <c>evaluation/*</c> and <c>test[/*]</c> route prefixes (Golden dataset,
/// fixtures, runtime verification, AITest smoke, etc.). Production therefore returns
/// 404 because the endpoints do not exist, rather than relying on a spoofable request signal.
/// </summary>
public sealed class ProductionEvaluationRouteConvention : IApplicationModelConvention
{
	public void Apply(ApplicationModel application)
	{
		for (var index = application.Controllers.Count - 1; index >= 0; index--)
		{
			var controller = application.Controllers[index];
			if (controller.Selectors.Any(selector =>
				IsEvaluationRoute(selector.AttributeRouteModel?.Template)))
			{
				application.Controllers.RemoveAt(index);
			}
		}
	}

	public static bool IsEvaluationRoute(string? routeTemplate)
	{
		if (string.IsNullOrWhiteSpace(routeTemplate))
			return false;

		var normalized = routeTemplate.TrimStart('/');
		return normalized.Equals("evaluation", StringComparison.OrdinalIgnoreCase) ||
			normalized.StartsWith("evaluation/", StringComparison.OrdinalIgnoreCase) ||
			normalized.Equals("test", StringComparison.OrdinalIgnoreCase) ||
			normalized.StartsWith("test/", StringComparison.OrdinalIgnoreCase);
	}
}
