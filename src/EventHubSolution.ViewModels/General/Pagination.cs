namespace EventHubSolution.ViewModels.General
{
    public class Pagination<T>
    {
        public List<T> Items { get; set; }

        public Metadata Metadata { get; set; }
    }
}
