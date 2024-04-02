using EventHubSolution.ViewModels.Constants;
using System.ComponentModel;

namespace EventHubSolution.ViewModels.Systems
{
    public class EventPaginationFilter : PaginationFilter
    {
        private EventType _type = EventType.ALL;
        [DefaultValue(EventType.ALL)]
        public EventType type
        {
            get
            {
                return _type;
            }
            set
            {
                _type = value;
            }
        }

        private string? _location;
        [DefaultValue(null)]
        public string? location
        {
            get
            {
                return _location;
            }
            set
            {
                _location = value;
            }
        }

        private PriceRangeVm? _priceRange;
        [DefaultValue(null)]
        public PriceRangeVm? priceRange
        {
            get
            {
                return _priceRange;
            }
            set
            {
                _priceRange = value;
            }
        }

        private List<string>? _categoryIds;
        [DefaultValue(null)]
        public List<string>? categoryIds
        {
            get
            {
                return _categoryIds;
            }
            set
            {
                _categoryIds = value;
            }
        }
    }
}
