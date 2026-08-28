using SuperBuilder_AI.Models.Metadata;


namespace SuperBuilder_AI.Interfaces;


public interface IMetadataSearchService
{


	Task<List<MetadataSearchResult>> SearchAsync(
		string question,
		long tenantId);


}