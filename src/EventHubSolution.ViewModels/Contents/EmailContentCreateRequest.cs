using Microsoft.AspNetCore.Http;

namespace EventHubSolution.ViewModels.Contents
{
    public class EmailContentCreateRequest
    {
        public string Content { get; set; }

        public List<IFormFile>? Attachments { get; set; }
    }
}
