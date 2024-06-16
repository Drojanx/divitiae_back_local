using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.VisualBasic;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Dynamic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace divitiae_api.Services
{
    public class ItemActivityLogServices : IItemActivityLogServices
    {

        private readonly IMongoCollection<ItemActivityLog> _itemActivityLogCollection;
        private readonly IMongoCollection<BsonDocument> _genericItemCollection;
        private readonly IMongoDatabase _mongoDivitiaeDatabase;
        private readonly MongoClient _divitiaeClient;


        public ItemActivityLogServices(IOptions<MongoDBSettings> mongoDBSettings)
        {
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            _mongoDivitiaeDatabase = _divitiaeClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
            _itemActivityLogCollection = _mongoDivitiaeDatabase.GetCollection<ItemActivityLog>("ItemsActivityLogs");
        }

        /// <summary>
        /// Inserta un itemActivityLog en base de datos usando una sesión de MongoDB
        /// </summary>
        /// <param name="log"></param>
        /// <returns>ItemActivityLog</returns>
        public async Task<ItemActivityLogDTO> InsertItemActivityLog(ItemActivityLog log)
        {
            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    await _itemActivityLogCollection.InsertOneAsync(log);
                    ItemActivityLogDTO item = new ItemActivityLogDTO()
                    {
                        Id = log.Id.ToString(),
                        ItemId = log.ItemId,
                        AppId = log.AppId,
                        CreatorId = log.CreatorId,
                        UnixCreatedOn   = log.UnixCreatedOn,
                        LogText = log.LogText
                    };
                    return item;
                }
                catch (Exception ex)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Devuelve una lista de los itemActivityLog del item con ID itemID
        /// </summary>
        /// <param name="itemId"></param>
        /// <returns>Lista de ItemActivityLog</returns>
        public async Task<List<ItemActivityLog>> GetItemLogs(string itemId)
        {
            return await _itemActivityLogCollection.Find(x => x.ItemId == itemId).ToListAsync();
        }
    }
}
