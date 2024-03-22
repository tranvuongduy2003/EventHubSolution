namespace EventHubSolution.ViewModels.Contents
{
    public class FileStorageVm
    {
        public string Id { get; set; }

        public string FileName { get; set; }

        public string FilePath { get; set; }

        public string FileType { get; set; }

        public long FileSize { get; set; }

        public DateTime CreatedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }
    }
}
