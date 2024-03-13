using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace EventHubSolution.BackendServer.Services
{
    public class SequenceService : ISequenceService
    {
        private readonly IConfiguration _configuration;

        public SequenceService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<int> GetKnowledgeBaseId()
        {
            using (SqlConnection conn = new SqlConnection(_configuration.GetConnectionString("DefaultConnection")))
            {
                if (conn.State == ConnectionState.Closed)
                {
                    await conn.OpenAsync();
                }

                var result = await conn.ExecuteScalarAsync<int>(@"SELECT (NEXT VALUE FOR KnowledgeBaseSequence)", null, null, 120, CommandType.Text);
                return result;
            }
        }
    }
}
