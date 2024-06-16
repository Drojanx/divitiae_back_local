using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace divitiae_api.Services.Interfaces
{
    public interface IUserToWorkEnvRoleServices
    {
        Task<bool> UserToWorkEnvRoleExists(string userId, string weId);        
        Task InsertUserToWorkEnvRole(UserToWorkEnvRole uToWERole);
        Task DeleteUserToWorkEnvRole(string userId, string weId);
        Task CanModifyWorkEnvironment(string userId, string weId);
        Task<bool> UserIsOwner(string workEnvId, string userId);
        Task<List<UserToWorkEnvRole>> GetAllUserToWorkEnvRoleByWorkEnvId(Guid workEnvId);
        Task<UserToWorkEnvRole> GetUserToWorkEnvRoleByUserIdAndWorkEnvId(string workEnvId, string userId);
        Task<WorkEnvironmentDTO> CanAccessWorkEnvironment(string userId, string weId);
        Task<List<UserToWorkEnvRole>> GetEveryOwnerRole(string weId);
    }
}
