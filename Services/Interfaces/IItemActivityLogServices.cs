using divitiae_api.Models;
using MongoDB.Driver;
using System.ComponentModel;
using System.Text.Json.Nodes;

namespace divitiae_api.Services.Interfaces
{
    public interface IItemActivityLogServices
    {
        Task<ItemActivityLog> InsertItemActivityLog(ItemActivityLog log);
        Task<List<ItemActivityLog>> GetItemLogs(string itemId);
    }
}
