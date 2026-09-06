using System.Collections.Generic;
using SuperBuilder_AI.Models.BI;
using SuperBuilder_AI.Services.BI;
using Xunit;

namespace SuperBuilder_AI.Tests;

/// <summary>M6-05 脱敏工具测试（无 LLM / 无 DB，确定性）。</summary>
public class AskPiiRedactorTests
{
	private readonly AskPiiRedactor _redactor = AskPiiRedactor.Default;

	[Fact]
	public void RedactText_MasksPiiKeywords_And_Phone_And_Email()
	{
		var input = "我的 phone 是 13800138000，邮箱 a@b.com 查销售额";
		var outp = _redactor.RedactText(input);

		Assert.Contains("***", outp);
		Assert.DoesNotContain("phone", outp);
		Assert.DoesNotContain("13800138000", outp);
		Assert.DoesNotContain("a@b.com", outp);
		// 非敏感业务词保留。
		Assert.Contains("销售额", outp);
	}

	[Fact]
	public void RedactText_NonSensitive_Passthrough()
	{
		var input = "给我最近的十张入库凭证";
		Assert.Equal(input, _redactor.RedactText(input));
	}

	[Fact]
	public void RedactSql_MasksPiiColumns()
	{
		var sql = "SELECT salary, name FROM emp WHERE email = 'x@y.com'";
		var outp = _redactor.RedactSql(sql);

		Assert.Contains("***", outp);
		Assert.DoesNotContain("salary", outp);
		// 非 PII 列名与结构保留。
		Assert.Contains("name", outp);
		Assert.Contains("FROM emp", outp);
	}

	[Fact]
	public void RedactResultSample_MasksPiiColumnValues_KeepsNonPii()
	{
		var data = new QueryResult
		{
			Rows = new List<Dictionary<string, object?>>
			{
				new() { ["name"] = "张三", ["salary"] = 9999, ["come_time"] = "2026-01-01" }
			}
		};

		var sample = _redactor.RedactResultSample(data);

		Assert.Contains("rows=1", sample);
		Assert.Contains("salary=***", sample);
		Assert.Contains("name=张三", sample);
		Assert.Contains("come_time=2026-01-01", sample);
	}

	[Fact]
	public void RedactResultSample_Empty_ReturnsRowsZero()
	{
		Assert.Equal("rows=0", _redactor.RedactResultSample(null));
		Assert.Equal("rows=0", _redactor.RedactResultSample(new QueryResult()));
	}
}
