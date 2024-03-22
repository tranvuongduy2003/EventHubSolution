namespace EventHubSolution.ViewModels.Contents
{
    public class EmailContentVm
    {
        public string Id { get; set; }

        public string EventId { get; set; }

        public string Content { get; set; }

        public List<FileStorageVm> Attachments { get; set; }
    }
}
