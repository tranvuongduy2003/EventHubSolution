namespace EventHubSolution.ViewModels.General
{
    public class Metadata
    {
        public int CurrentPage { get; private set; }
        public int TotalPages { get; private set; }
        public bool TakeAll { get; private set; }
        public int PageSize { get; private set; }
        public int TotalCount { get; private set; }
        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;

        public Metadata()
        {
            TotalCount = 0;
            PageSize = 10;
            CurrentPage = 1;
            TotalPages = 0;
            TakeAll = true;
        }
        public Metadata(int count, int pageNumber, int pageSize, bool takeAll)
        {
            TotalCount = count;
            PageSize = pageSize;
            CurrentPage = pageNumber;
            TotalPages = (int)Math.Ceiling(count / (double)pageSize);
            TakeAll = takeAll;
        }
    }
}
