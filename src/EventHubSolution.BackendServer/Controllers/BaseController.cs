using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventHubSolution.BackendServer.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize("Bearer")]
    public class BaseController : ControllerBase
    {
    }
}
