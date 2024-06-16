using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using MongoDB.Bson;
using MongoDB.Driver;

namespace divitiae_api.Services.Interfaces
{
    public interface IWsAppsRelationServices
    {
        Task InsertWsAppsRelation(WsAppsRelation wsAppsRelation);
        Task<WsAppsRelation> GetWsAppsRelationByWsAndAppId(string wsId, string appId);
        Task DeleteWsAppsRelation(string wsId, string appId);

    }
}
