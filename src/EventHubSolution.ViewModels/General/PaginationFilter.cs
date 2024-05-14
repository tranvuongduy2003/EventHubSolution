using EventHubSolution.ViewModels.Constants;
using System.ComponentModel;

namespace EventHubSolution.ViewModels.General
{
    public class PaginationFilter
    {
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


        private int _size = 10;
        [DefaultValue(10)]
        public int size
        {
            get
            {
                return _size;
            }
            set
            {
                _size = value < 1 ? 1 : value;
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
                _order = value;
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
