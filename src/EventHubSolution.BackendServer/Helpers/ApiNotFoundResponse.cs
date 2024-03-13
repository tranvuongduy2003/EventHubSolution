namespace EventHubSolution.BackendServer.Helpers
{
    public class ApiNotFoundResponse : ApiResponse
    {
        public ApiNotFoundResponse(string message)
           : base(404, message)
        {
        }
    }
}
