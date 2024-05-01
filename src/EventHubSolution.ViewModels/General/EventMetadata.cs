namespace EventHubSolution.ViewModels.General
{
    public class EventMetadata : Metadata
    {
        public int TotalPublic { get; private set; }

        public int TotalPrivate { get; private set; }

        public int TotalTrash { get; private set; }

        public EventMetadata() : base()
        {
            TotalPublic = 0;
            TotalPrivate = 0;
            TotalTrash = 0;
        }

        public EventMetadata(int count, int pageNumber, int pageSize, bool takeAll, int totalPublic, int totalPrivate, int totalTrash) : base(count, pageNumber, pageSize, takeAll)
        {
            TotalPublic = totalPublic;
            TotalPrivate = totalPrivate;
            TotalTrash = totalTrash;
        }
    }
}
