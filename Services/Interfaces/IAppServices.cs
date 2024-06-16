using divitiae_api.Models;
using MongoDB.Driver;

namespace divitiae_api.Services.Interfaces
{
    public interface IAppServices
    {
        Task InsertApp(App ap, IClientSessionHandle session);
        Task InsertApp(App app);
        Task<List<App>> InsertWelcomeApps(IClientSessionHandle session, Workspace ws);
        Task<List<Item>> InsertWelcomeClientsApp(App app, IClientSessionHandle session);
        Task InsertWelcomeInvoicesApp(App app, App sampleClientsApp, List<Item> sampleClientsItems, IClientSessionHandle session);
        //Task UpdateApp(App app, IClientSessionHandle session);
        Task UpdateApp(App app, IClientSessionHandle session);
        Task UpdateApp(App app);
        Task DeleteApp(string id);
        Task<List<App>> GetAllApps();
        Task<App> GetAppById(string id);
        Task<AppDTO> GetAppDTO(string id);
        Task CanCreateApp(string userId, string workspaceId);
        Task CanAccessApp(string userId, string appId);
        Task CanModifyApp(string userId, string appId);
    }
}
