using AutoMapper;

namespace EventHubSolution.BackendServer.Helpers
{
    public class MappingConfig
    {
        public static MapperConfiguration RegisterMaps()
        {
            var mappingConfig = new MapperConfiguration(config =>
            {
            });
            return mappingConfig;
        }
    }
}
