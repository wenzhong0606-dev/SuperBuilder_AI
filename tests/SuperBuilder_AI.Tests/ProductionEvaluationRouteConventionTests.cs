using System;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using SuperBuilder_AI.Api.Security;
using Xunit;

namespace SuperBuilder_AI.Tests;

public sealed class ProductionEvaluationRouteConventionTests
{
	[Fact]
	public void Apply_RemovesEvaluationControllersAndKeepsOrdinaryControllers()
	{
		var application = new ApplicationModel();
		application.Controllers.Add(BuildController(typeof(EvaluationStubController), "evaluation/golden-runtime"));
		application.Controllers.Add(BuildController(typeof(ApiStubController), "api/stub"));

		new ProductionEvaluationRouteConvention().Apply(application);

		var remaining = Assert.Single(application.Controllers);
		Assert.Equal(typeof(ApiStubController), remaining.ControllerType.AsType());
	}

	[Theory]
	[InlineData("evaluation/golden-runtime")]
	[InlineData("/evaluation/diagnostics")]
	[InlineData("EVALUATION/business-entity")]
	[InlineData("test")]
	[InlineData("test/foo")]
	public void IsEvaluationRoute_MatchesEvaluationControllers(string template)
	{
		Assert.True(ProductionEvaluationRouteConvention.IsEvaluationRoute(template));
	}

	[Theory]
	[InlineData(null)]
	[InlineData("")]
	[InlineData("api/metadata-vector")]
	public void IsEvaluationRoute_DoesNotMatchOtherControllers(string? template)
	{
		Assert.False(ProductionEvaluationRouteConvention.IsEvaluationRoute(template));
	}

	private static ControllerModel BuildController(Type type, string route)
	{
		var controller = new ControllerModel(type.GetTypeInfo(), Array.Empty<object>());
		controller.Selectors.Add(new SelectorModel
		{
			AttributeRouteModel = new AttributeRouteModel(new RouteAttribute(route))
		});
		return controller;
	}

	private sealed class EvaluationStubController;
	private sealed class ApiStubController;
}
