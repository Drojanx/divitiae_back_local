using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using MongoDB.Driver;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace divitiae_api.Services.Interfaces
{
    public interface IItemServices
    {
        Task<Item> InsertItem(Item item, string appId, IClientSessionHandle session);
        Task<Item> InsertItem(Item item, string appId);
        //Task ProbatinaInsertItem(Item item, string appId);
        Task UpdateItem(Item item, string appId, IClientSessionHandle session);
        Task UpdateItem(Item item, string appId);
        Task AddRelation(string appId, string id, RelatedItemDTO rel, IClientSessionHandle session);
        Task RemoveRelation(string appId, string id, RelatedItemDTO rel, IClientSessionHandle session);
        Task DeleteItem(string appId, string id, IClientSessionHandle session);
        Task DeleteItem(string appId, string id);
        Task BulkDelete(string appId, List<string> ids);
        Task<Item> GetAppItemById(string itemId, string appId);
        Task<List<ItemDTO>> GetAppItems(string appId);
        Task<List<ItemDTO>> GetAppItemsPaginated(string appId, int offset, bool ascending);
        Task<List<ItemDTO>> GetAppItemsByName(string descriptiveName, string appId);
        //Task<List<ItemDTO>> GetAppItemsFiltered(string appId, IEnumerable<FilterObject> filters);
        Task<List<Item>> GenerateSampleClients(App app, IClientSessionHandle session);
        Task<List<Item>> GenerateSampleInvoices(App app, App sampleClientsApp, List<Item> sampleClientsItems, IClientSessionHandle session);
    }
}
