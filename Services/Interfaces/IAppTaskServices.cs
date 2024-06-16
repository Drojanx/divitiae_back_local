using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using MongoDB.Driver;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace divitiae_api.Services.Interfaces
{
    public interface IAppTaskServices
    {
        Task<AppTask> InsertAppTask(AppTask task);
        Task UpdateAppTask(AppTask task);
        Task DeleteAppTask(string taskId, IClientSessionHandle session);
        Task DeleteAppTask(string taskId);
        Task<AppTask> GetAppTaskById(string taskId);
        Task<List<AppTask>> GetAppTasksByUser(string userId);
        Task<List<AppTask>> GetAppTaskByUserAndEnvironment(string userId, string environmentId);
        Task<List<AppTask>> GetAppTasksByItem(string itemId);
        Task<List<AppTask>> GetAppTasksByApp(string appId);
        Task<List<AppTask>> GetAppTasksByWorkspace(string workspaceId);
        Task<AppTask> mapAppTaskDTOCreate(User user, AppTaskDTOCreate taskDTO);
    }
}
