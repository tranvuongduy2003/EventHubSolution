using EventHubSolution.ViewModels.Constants;
using System.ComponentModel;

namespace EventHubSolution.ViewModels.General
{
    public class PaginationFilter
    {
        const int maxPageSize = 10;

        private int _page = 1;
        [DefaultValue(1)]
        public int page
        {
            get
            {
                return _page;
            }
            set
            {
                _page = value < 1 ? 1 : value;
            }
        }


        private int _size = maxPageSize;
        [DefaultValue(maxPageSize)]
        public int size
        {
            get
            {
                return _size;
            }
            set
            {
                _size = value > maxPageSize ? maxPageSize : value;
            }
        }

        private bool _takeAll = true;
        [DefaultValue(true)]
        public bool takeAll
        {
            get
            {
                return _takeAll;
            }
            set
            {
                _takeAll = value;
            }
        }

        private PageOrder _order = PageOrder.ASC;
        public PageOrder order
        {
            get
            {
                return _order;
            }
            set
            {
                _order = value != PageOrder.ASC || value != PageOrder.DESC ? PageOrder.ASC : value;
            }
        }

        public string? _search = null;
        [DefaultValue(null)]
        public string? search
        {
            get
            {
                return _search;
            }
            set
            {
                _search = value;
            }
        }
    }
}
