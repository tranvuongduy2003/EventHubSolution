using EventHubSolution.BackendServer.Constants;
using Microsoft.AspNetCore.Mvc;

namespace EventHubSolution.BackendServer.Authorization
{
    public class ClaimRequirementAttribute : TypeFilterAttribute
    {
        public ClaimRequirementAttribute(FunctionCode functionId, CommandCode commandId)
            : base(typeof(ClaimRequirementFilter))
        {
            Arguments = new object[] { functionId, commandId };
        }
    }
}
