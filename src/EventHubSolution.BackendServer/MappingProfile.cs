using AutoMapper;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.ViewModels.Contents;

namespace EventHubSolution.BackendServer
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            CreateMap<FileStorage, FileStorageVm>().ReverseMap();
        }
    }
}
