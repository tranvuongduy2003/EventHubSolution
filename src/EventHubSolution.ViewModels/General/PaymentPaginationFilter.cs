using EventHubSolution.ViewModels.Constants;
using System.ComponentModel;

namespace EventHubSolution.ViewModels.General
{
    public class PaymentPaginationFilter : PaginationFilter
    {
        private PaymentStatus? _status = PaymentStatus.ALL;
        [DefaultValue(PaymentStatus.ALL)]
        public PaymentStatus? status
        {
            get
            {
                return _status;
            }
            set
            {
                _status = value;
            }
        }
    }
}
