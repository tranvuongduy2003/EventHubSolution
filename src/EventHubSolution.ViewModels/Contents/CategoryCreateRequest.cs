using Microsoft.AspNetCore.Http;

namespace EventHubSolution.ViewModels.Contents
{
    public class CategoryCreateRequest
    {
        public string Name { get; set; }

        // Image name
        public IFormFile IconImage { get; set; }

        public string Color { get; set; }
    }
}
