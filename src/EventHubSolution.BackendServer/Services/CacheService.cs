using EventHubSolution.BackendServer.Services.Interfaces;
using Newtonsoft.Json;
using StackExchange.Redis;

namespace EventHubSolution.BackendServer.Services
{
    public class CacheService : ICacheService
    {
        private readonly IConfiguration _configuration;
        private IDatabase _cacheDb;

        public CacheService(IConfiguration configuration)
        {
            _configuration = configuration;
            var redis = ConnectionMultiplexer.Connect(_configuration.GetConnectionString("RedisCache"));
            _cacheDb = redis.GetDatabase();
        }

        public T GetData<T>(string key)
        {
            var value = _cacheDb.StringGet(key);
            if (!string.IsNullOrEmpty(value))
                return JsonConvert.DeserializeObject<T>(value);

            return default;
        }

        public object RemoveData(string key)
        {
            var _exist = _cacheDb.KeyExists(key);
            if (_exist)
                return _cacheDb.KeyDelete(key);

            return false;
        }

        public bool SetData<T>(string key, T value, DateTimeOffset expirationTime)
        {
            var expiryTime = expirationTime.DateTime.Subtract(DateTime.Now);
            return _cacheDb.StringSet(key, JsonConvert.SerializeObject(value), expiryTime);
        }
    }
}
