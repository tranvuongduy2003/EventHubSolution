namespace EventHubSolution.BackendServer.Helpers
{
    public class ApiUnauthorizedResponse : ApiResponse
    {
        public ApiUnauthorizedResponse(string message)
           : base(401, message)
        {
        }
    }
}
