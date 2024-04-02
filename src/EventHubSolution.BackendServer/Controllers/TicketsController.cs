using EventHubSolution.BackendServer.Data;

namespace EventHubSolution.BackendServer.Controllers
{
    public class TicketsController : BaseController
    {
        private readonly ApplicationDbContext _db;

        public TicketsController(ApplicationDbContext db)
        {
            _db = db;
        }
    }
}
