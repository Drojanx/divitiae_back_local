using divitiae_api.Models;
using divitiae_api.Services.Interfaces;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using System.Text.Json;
using static System.Net.Mime.MediaTypeNames;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Serializers;
using divitiae_api.Controllers;
using divitiae_api.Models.Exceptions;
using divitiae_api.Models.DTOs;
using System.Collections.Generic;

namespace divitiae_api.Services
{
    public class ItemServices : IItemServices
    {
        private readonly IMongoCollection<Item> _itemCollection;
        private readonly IMongoCollection<BsonDocument> _genericItemCollection;
        private readonly IMongoDatabase _divitiaeDatabase;
        private readonly MongoClient _divitiaeClient ;
        private readonly Lazy<IAppServices> _appServices;


        public ItemServices(IOptions<MongoDBSettings> mongoDBSettings, Lazy<IAppServices> appServices)
        {
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            IMongoDatabase database = _divitiaeClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
            _divitiaeDatabase = database;
            _itemCollection = database.GetCollection<Item>("Items");
            _genericItemCollection = database.GetCollection<BsonDocument>("Items");
            _appServices = appServices;
        }

        
        /// <summary>
        /// Crea los items de ejemplo de una de las app de ejemplo usando una sesión de MongoDB. Luego, devuelve
        /// una lista con dichos items.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="session"></param>
        /// <returns>Lista de Items</returns>
        public async Task<List<Item>> GenerateSampleClients(App app, IClientSessionHandle session)
        {
            List<Item> clientsItems = new List<Item>();

            for (int i = 1; i < 30; i++)
            {
                List<FieldValue> fieldsValue = new List<FieldValue>();
                var active = i%4 == 0 ? false : true;
                DateTime date = DateTime.Today.AddDays(-i*2);
                double total = i * 5 - i * 2;

                FieldValue value1 = new FieldValue(app.Fields.ElementAt(0), "Client " + i); 
                FieldValue value2 = new FieldValue(app.Fields.ElementAt(1), active); 
                FieldValue value3 = new FieldValue(app.Fields.ElementAt(2), ((DateTimeOffset)date).ToUnixTimeSeconds()); 
                FieldValue value4 = new FieldValue(app.Fields.ElementAt(3), total); 
                FieldValue value5 = new FieldValue(app.Fields.ElementAt(4), 0);

                fieldsValue.AddRange(new List<FieldValue> { value1, value2, value3, value4, value5});

                Item thisItem = new Item() { DescriptiveName = "Client Name " + i ,FieldsValue = fieldsValue};

                //await InsertItem(thisItem, app.StringId);

                clientsItems.Add(thisItem);
            }

            return clientsItems;
            
        }

        /// <summary>
        /// Crea los items de ejemplo de la segunda app de ejemplo usando una sesión de MongoDB. También recibe la otra app y su lista de items
        /// como argumentos, ya que estos items generan relación con los otros.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="sampleClientsApp"></param>
        /// <param name="sampleClientsItems"></param>
        /// <param name="session"></param>
        /// <returns>Lista de Items</returns>
        public async Task<List<Item>> GenerateSampleInvoices(App app, App sampleClientsApp, List<Item> sampleClientsItems, IClientSessionHandle session)
        {
            List<Item> invoicesItems = new List<Item>();
            Random rnd = new Random();
            var clientCollection = _divitiaeDatabase.GetCollection<Item>(sampleClientsApp.Id.ToString());
            var invoiceCollection = _divitiaeDatabase.GetCollection<Item>(app.Id.ToString());
            for (int i = 1; i < 60; i++)
            {
                List<FieldValue> fieldsValue = new List<FieldValue>();
                List<FieldRelationValue> fieldsRelationValue = new List<FieldRelationValue>();

                Item thisItem = new Item();
                var active = i % 4 == 0 ? false : true;
                DateTime date = DateTime.Today.AddDays(-i * 2.5);
                double total = i * 5 - i * 2;
                int randomClientIndex = rnd.Next(28);
                string clientRelatedId = sampleClientsItems[randomClientIndex].Id.ToString();
                string clientRelatedName = sampleClientsItems[randomClientIndex].DescriptiveName;
                List<RelatedItem> relatedItems = new List<RelatedItem>() { new RelatedItem(clientRelatedName, clientRelatedId) };
                ItemRelation itemRelations = new ItemRelation(sampleClientsApp.Id.ToString(), sampleClientsApp.AppName, relatedItems);
                FieldValue value6 = new FieldValue(app.Fields.ElementAt(0), "This is a description for invoice number " + i);
                FieldValue value7 = new FieldValue(app.Fields.ElementAt(1), active);
                FieldValue value8 = new FieldValue(app.Fields.ElementAt(2), ((DateTimeOffset)date).ToUnixTimeSeconds());
                FieldValue value9 = new FieldValue(app.Fields.ElementAt(3), total);
                FieldRelationValue value10 = new FieldRelationValue(app.RelationFields.ElementAt(0).Id, app.RelationFields.ElementAt(0).Name, app.RelationFields.ElementAt(0).NameAsProperty, app.RelationFields.ElementAt(0).Type, itemRelations);

                fieldsValue.AddRange(new List<FieldValue> { value6, value7, value8, value9 });
                fieldsRelationValue.Add(value10);

                thisItem.FieldsValue = fieldsValue;
                thisItem.FieldsRelationValue = fieldsRelationValue;
                Item relatedClient = sampleClientsItems[randomClientIndex];
                thisItem.DescriptiveName = "Invoice " + i + " - " + relatedClient.DescriptiveName;

                await invoiceCollection.InsertOneAsync(session, thisItem);
                ItemRelation relation = relatedClient.Relations.FirstOrDefault(rel => rel.RelatedAppId == app.Id.ToString());
                if (relation == null)
                {
                    relation = new ItemRelation(app.Id.ToString(), app.AppName, new List<RelatedItem>());
                    relatedClient.Relations.Add(relation);
                }

                relation.RelatedItems.Add(new RelatedItem(thisItem.DescriptiveName, thisItem.Id.ToString()));

                var filter2 = Builders<Item>
                    .Filter
                    .Eq(s => s.Id, sampleClientsItems[randomClientIndex].Id);
                await clientCollection.ReplaceOneAsync(session, filter2, relatedClient);

                invoicesItems.Add(thisItem);
            }

            return invoicesItems;

        }

        /// <summary>
        /// Devuelve una lista de todos los items de la app con ID appID mapeados a itemDTO.
        /// </summary>
        /// <param name="appId"></param>
        /// <returns>Lista de ItemDTO</returns>
        public async Task<List<ItemDTO>> GetAppItems(string appId)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            List<Item> items = await itemAppCollection.FindAsync(new BsonDocument()).Result.ToListAsync();
            List<ItemDTO> itemsDTO = new List<ItemDTO>();
            foreach (Item item in items)
            {
                ItemDTO itemDTO = new ItemDTO
                {
                    Id = item.Id.ToString(),
                    DescriptiveName = item.DescriptiveName,
                    FieldsValue = item.FieldsValue,
                    FieldsRelationValue = item.FieldsRelationValue,
                    Relations = item.Relations
                };
                itemsDTO.Add(itemDTO);
            }
            return itemsDTO;
        }

        /// <summary>
        /// Devuelve una lista algunos items de la app con ID appID mapeados a itemDTO. Devolverá los items
        /// siguientes al número de items que indique el argumento offset. También revisa si la lista de items
        /// deberá extraerse en orden ascendente o descendente de fecha de creación.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="offset"></param>
        /// <param name="ascending"></param>
        /// <returns>Lista de ItemDTO</returns>
        public async Task<List<ItemDTO>> GetAppItemsPaginated(string appId, int offset, bool ascending)
        {
            const int limit = 10;
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            List<Item> items = new List<Item>();


            var sortDefinition = ascending
            ? Builders<Item>.Sort.Ascending("Id")
            : Builders<Item>.Sort.Descending("Id");

            items = await itemAppCollection.Find(FilterDefinition<Item>.Empty)
                                        .Sort(sortDefinition)
                                        .Skip(offset)
                                        .Limit(limit)
                                        .ToListAsync();

            
            
            List<ItemDTO> itemsDTO = new List<ItemDTO>();
            foreach(Item item in items)
            {
                ItemDTO itemDTO = new ItemDTO
                {
                    Id = item.Id.ToString(),
                    DescriptiveName = item.DescriptiveName,
                    FieldsValue = item.FieldsValue,
                    FieldsRelationValue = item.FieldsRelationValue,
                    Relations = item.Relations
                };
                itemsDTO.Add(itemDTO);
            }
            return itemsDTO;
    }

        /// <summary>
        /// Devuelve el item con ID itemId que pertenece a la app con ID appId
        /// </summary>
        /// <param name="itemId"></param>
        /// <param name="appId"></param>
        /// <returns>Item</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<Item> GetAppItemById(string itemId, string appId)
        {
            await _appServices.Value.GetAppById(appId);

            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            return await itemAppCollection.Find(x => x.Id.ToString() == itemId).FirstOrDefaultAsync()
                ?? throw new ItemNotFoundException("Item", "Id", itemId);
            
        }

        /// <summary>
        /// Develve una lista de itemDTO obtenidos a partir de los items que devuelve una búsqueda en base
        /// de datos. Esta búsqueda se devolverá los items cuyo _descriptiveName contenga el valor del
        /// argumento descriptiveName
        /// </summary>
        /// <param name="descriptiveName"></param>
        /// <param name="appId"></param>
        /// <returns>Lista de ItemDTO</returns>
        public async Task<List<ItemDTO>> GetAppItemsByName(string descriptiveName, string appId)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            List<ItemDTO> itemsDTO = new List<ItemDTO>();
            
            var filterList = Builders<Item>.Filter.Regex("DescriptiveName", new MongoDB.Bson.BsonRegularExpression(descriptiveName, "i"));
            
        
            List<Item> items = await itemAppCollection.FindAsync(filterList).Result.ToListAsync();
            foreach (Item item in items)
            {
                ItemDTO itemDTO = new ItemDTO
                {
                    Id = item.Id.ToString(),
                    DescriptiveName = item.DescriptiveName,
                    FieldsValue = item.FieldsValue,
                    FieldsRelationValue = item.FieldsRelationValue,
                    Relations = item.Relations
                };
                itemsDTO.Add(itemDTO);
            }
            return itemsDTO;
        }


        //public async Task<List<ItemDTO>> GetAppItemsFiltered(string appId, IEnumerable<FilterObject> filters)
        //{
        //    var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

        //    List<ItemDTO> itemsDTO = new List<ItemDTO>();

        //    var filterBuilder = Builders<Item>.Filter;
        //    var filterList = filterBuilder.Empty;
        //    foreach (var filter in filters)
        //    {
        //        if (!string.IsNullOrEmpty(filter.FieldName) && !string.IsNullOrEmpty(filter.FieldValue))
        //        {
        //            //filterList &= Builders<Item>.Filter.Regex(filter.FieldName, new MongoDB.Bson.BsonRegularExpression(filter.FieldValue, "i"));
        //            filterList &= filterBuilder.ElemMatch(
        //                item => item.FieldsValue,
        //                Builders<FieldValue>.Filter.And(
        //                    Builders<FieldValue>.Filter.Eq(fv => fv.NameAsProperty, filter.FieldName),
        //                    Builders<FieldValue>.Filter.Regex(fv => fv.Value, new BsonRegularExpression(filter.FieldValue, "i"))
        //                )
        //            );
        //        }
        //    }
        //    List<Item> items = await itemAppCollection.FindAsync(filterList).Result.ToListAsync();
        //    foreach (Item item in items)
        //    {
        //        ItemDTO itemDTO = new ItemDTO
        //        {
        //            Id = item.Id.ToString(),
        //            DescriptiveName = item.DescriptiveName,
        //            FieldsValue = item.FieldsValue,
        //            FieldsRelationValue = item.FieldsRelationValue,
        //            Relations = item.Relations
        //        };
        //        itemsDTO.Add(itemDTO);
        //    }
        //    return itemsDTO;
        //}


        /// <summary>
        /// Inserta en base de datos un item en la app con ID appId, usando una sesión de MongoDB
        /// </summary>
        /// <param name="item"></param>
        /// <param name="appId"></param>
        /// <param name="session"></param>
        /// <returns>Item</returns>
        public async Task<Item> InsertItem(Item item, string appId, IClientSessionHandle session)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);                        
            await itemAppCollection.InsertOneAsync(session, item);
            App app = await _appServices.Value.GetAppById(appId);
            app.Items.Add(item.Id);
            await _appServices.Value.UpdateApp(app, session);
            return item;
        }

        /// <summary>
        /// Crea una sesión de MongoDB y llama al método anterior que inserta el item que recibe
        /// como argumento en la app con ID appId que recibe también como argumento
        /// </summary>
        /// <param name="item"></param>
        /// <param name="appId"></param>
        /// <returns>Item</returns>
        public async Task<Item> InsertItem(Item item, string appId)
        {
            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);
                    await itemAppCollection.InsertOneAsync(session, item);
                    App app = await _appServices.Value.GetAppById(appId);
                    app.Items.Add(item.Id);
                    await _appServices.Value.UpdateApp(app, session);
                    
                    foreach (FieldRelationValue frv in item.FieldsRelationValue)
                    {
                        List<RelatedItem> relatedItems = new List<RelatedItem>() { new RelatedItem(item.DescriptiveName, item.Id.ToString()) };

                        ItemRelation itemRelation = new ItemRelation(appId, app.AppName, relatedItems);
                        foreach (RelatedItem re in frv.Value.RelatedItems)
                        {
                            Item relatedItem = await GetAppItemById(re.RelatedItemId, frv.Value.RelatedAppId);
                            relatedItem.Relations.Add(itemRelation);
                            await UpdateItem(relatedItem, frv.Value.RelatedAppId    , session);
                        }
                    }



                    return item;
                } 
                catch (Exception)
                {
                    throw;
                }
            }

                
        }

        /// <summary>
        /// Actualiza en base de datos un item de la app con ID appId usando una sesión de MongoDB
        /// </summary>
        /// <param name="item"></param>
        /// <param name="appId"></param>
        /// <param name="session"></param>
        public async Task UpdateItem(Item item, string appId, IClientSessionHandle session)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            var filter = Builders<Item>
                .Filter
                .Eq(s => s.Id, item.Id);

            itemAppCollection.ReplaceOne(session, filter, item);
        }

        /// <summary>
        /// Crea una sesión de MongoDB y llama al método anterior que actualiza un item en base de datos.
        /// Antes de hacerlo, revisa los campos de relación comparandolos con la versión del item que
        /// hay desactualizada en base de datos: si encuentra algún valor nuevo en un campo de relación,
        /// busca el item de esta nueva relación e incluye una relación indirecta al item en cuestión; si
        /// ve que en el item actualizado falta alguna relación respecto al item en base de datos significa 
        /// que se ha eliminado una relación, así que busca el item de esta relación y elimina su relación
        /// indirecta con el item en cuestión.
        /// </summary>
        /// <param name="modItem"></param>
        /// <param name="appId"></param>
        public async Task UpdateItem(Item modItem, string appId)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            var filter = Builders<Item>
                .Filter
                .Eq(s => s.Id, modItem.Id);            

            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    Item item = itemAppCollection.Find(x => x.Id == modItem.Id).FirstOrDefault();

                    foreach (var itemRelation in item.FieldsRelationValue)
                    {
                        var relation = modItem.FieldsRelationValue.FirstOrDefault(x => x.Value.RelatedAppId == itemRelation.Value.RelatedAppId);

                        foreach (var relatedItem in itemRelation.Value.RelatedItems)
                        {
                            var relExistsInNew = relation.Value.RelatedItems.Find(x => x.RelatedItemId == relatedItem.RelatedItemId);

                            if (relExistsInNew == null)
                            {
                                await RemoveRelation(appId, item.Id.ToString(), new RelatedItemDTO(itemRelation.Value.RelatedAppId, relatedItem.RelatedItemName, relatedItem.RelatedItemId), session);
                            }
                        }
                    }

                    foreach (var itemRelation in modItem.FieldsRelationValue)
                    {
                        var relation = item.FieldsRelationValue.FirstOrDefault(x => x.Value.RelatedAppId == itemRelation.Value.RelatedAppId);

                        foreach (var relatedItem in itemRelation.Value.RelatedItems)
                        {
                            var relExistsInDb = relation.Value.RelatedItems.Find(x => x.RelatedItemId == relatedItem.RelatedItemId);

                            if (relExistsInDb == null)
                            {
                                await AddRelation(appId, item.Id.ToString(), new RelatedItemDTO(itemRelation.Value.RelatedAppId, relatedItem.RelatedItemName, relatedItem.RelatedItemId), session);
                            }
                        }
                    }

                    await itemAppCollection.ReplaceOneAsync(session, filter, modItem);

                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }

        }


        /// <summary>
        /// Busca en base de datos el item (item1) con ID id perteneciente a la app con ID appId. Luego, busca el item (item2)
        /// que representa el RelatedItemDTO rel en base de datos. A continuación, creará una relación directa de item1 a item2
        /// y una relación indirecta de item2 a item1. Luego, llama al método anterior que actualizará ambos items en base de datos
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="id"></param>
        /// <param name="rel"></param>
        /// <param name="session"></param>
        public async Task AddRelation(string appId, string id, RelatedItemDTO rel, IClientSessionHandle session)
        {
            App app = await _appServices.Value.GetAppById(appId);

            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);
            var relItemAppCollection = _divitiaeDatabase.GetCollection<Item>(rel.RelatedAppId);

            Item item = await itemAppCollection.Find(x => x.Id.ToString() == id).FirstOrDefaultAsync();

            Item relItem = await relItemAppCollection.Find(x => x.Id.ToString() == rel.RelatedItemId.ToString()).FirstOrDefaultAsync();

            item.FieldsRelationValue.FirstOrDefault(x => x.Value.RelatedAppId == rel.RelatedAppId).Value.RelatedItems.Add( new RelatedItem(rel.RelatedItemName, rel.RelatedItemId));
            var relItemRelations = relItem.Relations.FirstOrDefault(x => x.RelatedAppId == appId);
            if (relItemRelations != null)
            {
                relItemRelations.RelatedItems.Add(new RelatedItem(item.DescriptiveName, id));
            }
            if (relItemRelations == null)
            {
                List<RelatedItem> relatedItems = new List<RelatedItem>();
                relatedItems.Add(new RelatedItem(item.DescriptiveName, id));

                relItem.Relations.Add(new ItemRelation(appId, app.AppName, relatedItems));
            }
            await UpdateItem(item, appId, session);
            await UpdateItem(relItem, rel.RelatedAppId, session);
            
        }

        /// <summary>
        /// Busca en base de datos el item (item1) con ID id perteneciente a la app con ID appId. Luego, busca el item (item2)
        /// que representa el RelatedItemDTO rel en base de datos. A continuación, eliminará la relación directa que item1 tiene
        /// con item2, y eliminará también la relación indirecta que item2 tiene con item1. Luego, llama al método anterior que 
        /// actualizará ambos items en base de datos
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="id"></param>
        /// <param name="rel"></param>
        /// <param name="session"></param>
        public async Task RemoveRelation(string appId, string id, RelatedItemDTO rel, IClientSessionHandle session)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);
            var relItemAppCollection = _divitiaeDatabase.GetCollection<Item>(rel.RelatedAppId);

            Item item = await itemAppCollection.Find(x => x.Id.ToString() == id).FirstOrDefaultAsync();

            Item relItem = await relItemAppCollection.Find(x => x.Id.ToString() == rel.RelatedItemId.ToString()).FirstOrDefaultAsync();

            item.FieldsRelationValue.FirstOrDefault(x => x.Value.RelatedAppId == rel.RelatedAppId).Value.RelatedItems.RemoveAll(x => x.RelatedItemId == rel.RelatedItemId);
            relItem.Relations.FirstOrDefault(x => x.RelatedAppId == appId).RelatedItems.RemoveAll(x => x.RelatedItemId == id);

            await UpdateItem(item, appId, session);
            await UpdateItem(relItem, rel.RelatedAppId, session);

        }


        /// <summary>
        /// Elimina el item con ID itemId perteneciente a la app con ID appId en base de datos usando
        /// una sesión de MongoDB
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="itemId"></param>
        /// <param name="session"></param>
        public async Task DeleteItem(string appId, string itemId, IClientSessionHandle session)
        {
            var appCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            var filter = Builders<Item>.Filter.Eq(s => s.Id, new ObjectId(itemId));
            await appCollection.DeleteOneAsync(session, filter);
        }

        /// <summary>
        /// Elimina el item con ID itemId perteneciente a la app con ID app Id en base de datos.
        /// Antes de esto, llama al método DeleteAllItemsRelations para eliminar las relaciones
        /// indirectas que otros items puedan tener con él.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="itemId"></param>
        public async Task DeleteItem(string appId, string itemId)
        {

            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    App app = await _appServices.Value.GetAppById(appId);
                    var appCollection = _divitiaeDatabase.GetCollection<Item>(appId);

                    await DeleteAllItemsRelations(appId, itemId);

                    var filter = Builders<Item>.Filter.Eq(s => s.Id, new ObjectId(itemId));
                    await appCollection.DeleteOneAsync(session, filter);
                    string test = app.Items.Where(x => x.ToString() == itemId).FirstOrDefault().ToString();
                    app.Items.RemoveAll(i => i.ToString() == itemId);
                    await _appServices.Value.UpdateApp(app, session);
                }
                catch (Exception)
                {
                    throw;
                }

            }
        }

        /// <summary>
        /// Elimina los items cuyos IDs aparezcan en la lsita ids de la app con ID appId 
        /// en una sola llamada. También elimina las relaciones indirectas que otros items
        /// tengan con estos.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="ids"></param>
        public async Task BulkDelete(string appId, List<string> ids)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            foreach (var id in ids)
            {
                await DeleteAllItemsRelations(appId, id);
            }

            var filter = Builders<Item>.Filter.In("_id", ids.Select(i => i));
            await itemAppCollection.DeleteManyAsync(filter);
        }

        /// <summary>
        /// Revisa todas las relaciones del item con ID id perteneciente a la app con ID appId
        /// para luego eliminar las relaciones indirectas que otros items tengan con este. Luego,
        /// devuelve el item ya con sus relaciones eliminadas
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="id"></param>
        /// <returns>Item</returns>
        private async Task<Item> DeleteAllItemsRelations(string appId, string id)
        {
            var itemAppCollection = _divitiaeDatabase.GetCollection<Item>(appId);

            Item item = await itemAppCollection.Find(x => x.Id.ToString() == id).FirstOrDefaultAsync();

            foreach (var rel in item.Relations)
            {
                var relItemAppCollection = _divitiaeDatabase.GetCollection<Item>(rel.RelatedAppId);

                foreach (var relItem in rel.RelatedItems)
                {
                    Item relatedItem = await relItemAppCollection.Find(x => x.Id.ToString() == relItem.RelatedItemId).FirstOrDefaultAsync();
                    var relation = relatedItem.FieldsRelationValue.FirstOrDefault(x => x.Value.RelatedAppId == appId);
                    if (relation != null)
                    {
                        relation.Value.RelatedItems.RemoveAll(x => x.RelatedItemId == id);
                    }                    
                    await UpdateItem(relatedItem, rel.RelatedAppId);
                }
            }

            foreach (var rel in item.FieldsRelationValue)
            {
                var relItemAppCollection = _divitiaeDatabase.GetCollection<Item>(rel.Value.RelatedAppId);

                foreach (var relItem in rel.Value.RelatedItems)
                {
                    Item relatedItem = await relItemAppCollection.Find(x => x.Id.ToString() == relItem.RelatedItemId).FirstOrDefaultAsync();
                    var relation = relatedItem.Relations.FirstOrDefault(x => x.RelatedAppId == appId);
                    if (relation != null)
                    {
                        relation.RelatedItems.RemoveAll(x => x.RelatedItemId == id);
                    }
                    await UpdateItem(relatedItem, rel.Value.RelatedAppId);
                }
            }

            item.FieldsRelationValue.Clear();
            item.Relations.Clear();
            return item;

        }
    }
}
