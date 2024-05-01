using EventHubSolution.BackendServer.Data;
using EventHubSolution.BackendServer.Data.Entities;
using EventHubSolution.BackendServer.Services.Interfaces;
using EventHubSolution.ViewModels.Constants;
using Microsoft.EntityFrameworkCore;

namespace EventHubSolution.BackendServer.Services
{
    public class EntitiesService : IEntitiesService
    {
        private readonly ApplicationDbContext _db;
        private readonly ICacheService _cacheService;

        public EntitiesService(ApplicationDbContext _db, ICacheService cacheService)
        {
            _db = _db;
            _cacheService = cacheService;
        }

        public async Task<IEnumerable<Category>> GetCategoriesAsync()
        {
            

            return categories;
        }

        public async Task<IEnumerable<EventCategory>> GetEventCategoriesAsync()
        {
            // Check cache data
            var eventCategories = new List<EventCategory>();
            var cacheEventCategories = _cacheService.GetData<IEnumerable<EventCategory>>(CacheKey.EVENTCATEGORIES);
            if (cacheEventCategories != null && cacheEventCategories.Count() > 0)
                eventCategories = cacheEventCategories.ToList();
            else
                eventCategories = await _db.EventCategories.ToListAsync();
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<IEnumerable<EventCategory>>(CacheKey.EVENTCATEGORIES, eventCategories, expiryTime);

            return eventCategories;
        }

        public async Task<IEnumerable<Event>> GetEventsAsync()
        {
            // Check cache data
            var events = new List<Event>();
            var cacheEvents = _cacheService.GetData<IEnumerable<Event>>(CacheKey.EVENTS);
            if (cacheEvents != null && cacheEvents.Count() > 0)
                events = cacheEvents.ToList();
            else
                events = await _db.Events.ToListAsync();
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<IEnumerable<Event>>(CacheKey.EVENTS, events, expiryTime);

            return events;
        }

        public async Task<IEnumerable<Review>> GetReviewsAsync()
        {
            // Check cache data
            var reviews = new List<Review>();
            var cacheReviews = _cacheService.GetData<IEnumerable<Review>>(CacheKey.REVIEWS);
            if (cacheReviews != null && cacheReviews.Count() > 0)
                reviews = cacheReviews.ToList();
            else
                reviews = await _db.Reviews.ToListAsync();
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<IEnumerable<Review>>(CacheKey.REVIEWS, reviews, expiryTime);

            return reviews;
        }

        public async Task<IEnumerable<User>> GetUsersAsync()
        {
            // Check cache data
            var users = new List<User>();
            var cacheUsers = _cacheService.GetData<IEnumerable<User>>(CacheKey.USERS);
            if (cacheUsers != null && cacheUsers.Count() > 0)
                users = cacheUsers.ToList();
            else
                users = await _db.Users.ToListAsync();
            // Set expiry time
            var expiryTime = DateTimeOffset.Now.AddMinutes(45);
            _cacheService.SetData<IEnumerable<User>>(CacheKey.USERS, users, expiryTime);

            return users;
        }
    }
}
