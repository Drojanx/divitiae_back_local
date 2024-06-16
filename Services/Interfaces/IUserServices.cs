using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace divitiae_api.Services.Interfaces
{
    public interface IUserServices
    {
        Task<User> InsertUser(User user);
        //Task UpdateUser(IClientSessionHandle session, User user);
        Task UpdateUser(User user);
        Task DeleteUser(string id);
        Task<List<User>> GetAllUsers();
        Task<User> GetUserById(string id);
        //Task<User> GetUserByGoogleId(string id);
        Task<UserDataDTO> GetCurrentUserWithWorkEnvironmentsAndWorkspaces(User user);
        UserDataDTO GetUserPermissionsOnWorkEnvironment(User user, WorkEnvironment we);
        string HashPassword(string password);
        Task<User> GetUserByEmail(string email);
        Task<bool> UserByEmailExists(string email);
    }
}
