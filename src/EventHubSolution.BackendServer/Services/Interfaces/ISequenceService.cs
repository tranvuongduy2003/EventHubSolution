namespace EventHubSolution.BackendServer.Services
{
    public interface ISequenceService
    {
        Task<int> GetKnowledgeBaseId();
    }
}
