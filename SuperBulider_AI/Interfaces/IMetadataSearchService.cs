using SuperBulider_AI.Models.Metadata;


namespace SuperBulider_AI.Interfaces;


public interface IMetadataSearchService
{


	Task<List<MetadataSearchResult>> SearchAsync(
		string question,
		long tenantId);


}