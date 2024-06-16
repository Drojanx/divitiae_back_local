using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace divitiae_api.Services.Interfaces
{
    public interface IWorkspaceServices
    {
        Task InsertWorkspace(Workspace workspace);
        Task AddWorkspaceToEnvironment(Workspace workspace, string environmentId);
        //Task<Workspace> InsertWelcomeWorkspace(Workspace workspace);
        Task UpdateWorkspace(Workspace workspace);
        Task DeleteWorkspace(string id);
        Task<List<Workspace>> GetAllWorkspaces();
        Task<Workspace> GetWorkspaceById(string id);
        Task<WorkspaceDTO> GetWorkspaceDTO(string id);
        Task CanCreateWS(string userId, string workEnvironmentId);
        Task<WorkspaceDTO> CanAccessWS(string userId, string workspaceId);
        Task CanModifyWS(string userId, string workspaceId);
    }
}
