using divitiae_api.Models;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace divitiae_api.Services
{

    public class AppServices : IAppServices
    {

        private readonly SQLDataContext _context;
        private readonly IMongoCollection<App> _appCollection;
        private readonly IMongoDatabase _mongoDivitiaeDatabase;
        private readonly Lazy<IWorkspaceServices> _workspaceServices;
        private readonly Lazy<IItemServices> _itemServices;
        private readonly MongoClient _divitiaeClient;

        public AppServices(IOptions<MongoDBSettings> mongoDBSettings, SQLDataContext context, Lazy<IWorkspaceServices> workspaceServices, Lazy<IItemServices> itemServices)
        {
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            _context = context;
            _mongoDivitiaeDatabase = _divitiaeClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
            _workspaceServices = workspaceServices;
            _itemServices = itemServices;
            _appCollection = _mongoDivitiaeDatabase.GetCollection<App>("Apps"); ;
        }

        /// <summary>
        /// Borra una apliación de la base de datos según su ID
        /// </summary>
        /// <param name="id"></param>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task DeleteApp(string id)
        {
            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {
                App app = await GetAppById(id);
                await _mongoDivitiaeDatabase.DropCollectionAsync(id);
                var filter = Builders<App>.Filter.Eq(s => s.Id, new ObjectId(id));
                await _appCollection.DeleteOneAsync(filter);
                Workspace ws = _context.Workspaces.Include(ws => ws.WsAppsRelations).FirstOrDefault(ws => ws.Id.ToString() == app.WorkspaceId)
                    ?? throw new ItemNotFoundException("Workspace", "Id", app.WorkspaceId.ToString());
                if (ws.WsAppsRelations.Any(wsAppRel => wsAppRel.AppId == id))
                {
                    var fixedRels = ws.WsAppsRelations.Where(wsAppRel => wsAppRel.AppId == id);
                    ws.WsAppsRelations = ws.WsAppsRelations.Except(fixedRels).ToList();
                    _context.SaveChanges();
                }

            }

        }

        /// <summary>
        /// Devuelve un listado de todas las aplicaciones en base de datos
        /// </summary>
        /// <returns>Lista de aplicaciones</returns>
        public async Task<List<App>> GetAllApps()
        {
            return await _appCollection.FindAsync(new BsonDocument()).Result.ToListAsync();
        }

        /// <summary>
        /// Devuelve una app según su ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns>App</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<App> GetAppById(string id)
        {
            return await _appCollection.Find(x => x.Id.ToString() == id).FirstOrDefaultAsync()
                ?? throw new ItemNotFoundException("App", "Id", id);
            
        }

        /// <summary>
        /// Devuelve un DTO creado a partir de una app según su ID
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Un DTO de una app</returns>
        public async Task<AppDTO> GetAppDTO(string id)
        {
            App app = await GetAppById(id);

            AppDTO appDTO = new AppDTO()
            {
                Id = app.Id.ToString(),
                AppName = app.AppName,
                AppIconId = app.AppIconId,
                Fields = app.Fields,
                RelationFields = app.RelationFields
            };

            return appDTO;
            
        }

        /// <summary>
        /// Inserta una app nueva usando una sesión de MongoDB
        /// </summary>
        /// <param name="app"></param>
        /// <param name="session"></param>
        public async Task InsertApp(App app, IClientSessionHandle session)
        {
            var collectionExists = _mongoDivitiaeDatabase.ListCollectionNames().ToList().Contains(app.Id.ToString());
            if (collectionExists == false)
            {
                await _appCollection.InsertOneAsync(session, app);
                await _mongoDivitiaeDatabase.CreateCollectionAsync(session, app.Id.ToString());
            }
            else
            {
                Console.WriteLine("Collection Already Exists!!");
            }

        }

        /// <summary>
        /// Crea una sesón de MongoDB y llama al método anterior de insertar una app en base de datos
        /// </summary>
        /// <param name="app"></param>
        public async Task InsertApp(App app)
        {
            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    await InsertApp(app, session);
                }
                catch (Exception)
                {
                    throw;
                }
            }


        }

        /// <summary>
        /// Crea una sesión de MongoDB y las aplicaciones del entorno de ejemplo, llama a los métodos que las insertan y las devuelve en una lista
        /// </summary>
        /// <param name="session"></param>
        /// <returns>Lista de aplicaciones</returns>
        public async Task<List<App>> InsertWelcomeApps(IClientSessionHandle session, Workspace ws)
        {
            List<FieldStructure> sampleFieldsClients = new List<FieldStructure>
                {
                    new FieldStructure("Client Name", "string"),
                    new FieldStructure("Active", "boolean"),
                    new FieldStructure("Day of sale", "date"),
                    new FieldStructure("Total", "decimal"),
                    new FieldStructure("Discount", "decimal")
                };
            App clientsApp = new App() { AppName = "Clients Sample", AppIconId = "icon-55", WorkspaceId = ws.Id.ToString(),Fields = sampleFieldsClients };

            List<Item> ClientsSample = await InsertWelcomeClientsApp(clientsApp, session);

            List<FieldStructure> sampleFieldsInvoices = new List<FieldStructure>
                {
                    new FieldStructure("Description", "string"),
                    new FieldStructure("Paid", "boolean"),
                    new FieldStructure("Date", "date"),
                    new FieldStructure("Total", "decimal")
                };

            List<FieldStructureRelation> sampleFieldsRelationInvoices = new List<FieldStructureRelation>
                {
                    new FieldStructureRelation("Client", "itemRelation", clientsApp.Id.ToString(), clientsApp.AppName, ws.Id.ToString())
                };

            App invoicesApp = new App() { AppName = "Invoices Sample", AppIconId = "icon-180", WorkspaceId = ws.Id.ToString(), Fields = sampleFieldsInvoices, RelationFields = sampleFieldsRelationInvoices };

            await InsertWelcomeInvoicesApp(invoicesApp, clientsApp, ClientsSample, session);

            List<App> apps = new List<App>();

            apps.AddRange(new List<App> { clientsApp, invoicesApp });

            return apps;
            

        }

        /// <summary>
        /// Llama al método que crea los items de ejemplo, los incluye en la app de ejemplo y la inserta. Devuelve la lista de items de ejemplo.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="session"></param>
        /// <returns>Lista de items</returns>
        public async Task<List<Item>> InsertWelcomeClientsApp(App app, IClientSessionHandle session)
        {
           
            await InsertApp(app, session);
            var collection = _mongoDivitiaeDatabase.GetCollection<Item>(app.Id.ToString());

            List<Item> sampleItems = await _itemServices.Value.GenerateSampleClients(app, session);


            foreach (Item sampleItemClient in sampleItems)
            {
                await collection.InsertOneAsync(session, sampleItemClient);

                app.Items.Add(sampleItemClient.Id);
                await UpdateApp(app, session);
            }

            return sampleItems;
       
        }

        /// <summary>
        /// Llama al método que crea los items de ejemplo, los incluye en la app de ejemplo y la inserta. Devuelve la lista de items de ejemplo.
        /// En este caso, los items tienen relación a otra app así que recibe esta segunda app y la lista de items de esta app.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="sampleClientsApp"></param>
        /// <param name="sampleClientsItems"></param>
        /// <param name="session"></param>
        public async Task InsertWelcomeInvoicesApp(App app, App sampleClientsApp, List<Item> sampleClientsItems, IClientSessionHandle session)
        {
            //No puedo usar el método InsertApp ya que esto desencadenaría en una búsqueda del Workspace
            //para poder añadir la App al mismo, siendo quel el Workspace en cuestión aún no ha sido creado
            //(seguimos en la session de MongoDB)
            await InsertApp(app, session);
            //var collection = _mongoDivitiaeDatabase.GetCollection<Item>(app.StringId);
            //var clientCollection = _mongoDivitiaeDatabase.GetCollection<Item>(sampleClientsApp.StringId);

            List<Item> sampleItems = await _itemServices.Value.GenerateSampleInvoices(app, sampleClientsApp, sampleClientsItems, session);

            foreach (Item sampleItemInvoice in sampleItems)
            {
                app.Items.Add(sampleItemInvoice.Id);
                await UpdateApp(app, session);
            }
        }

        /// <summary>
        /// Actualiza una app en base de datos usando una sesión de MongoDB.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="session"></param>
        public async Task UpdateApp(App app, IClientSessionHandle session)
        {
            var filter = Builders<App>
                .Filter
                .Eq(s => s.Id, app.Id);

            await _appCollection.ReplaceOneAsync(filter, app);

        }

        /// <summary>
        /// Actualiza una app en base de datos.
        /// </summary>
        /// <param name="app"></param>
        public async Task UpdateApp(App app)
        {
            var filter = Builders<App>
                .Filter
                .Eq(s => s.Id, app.Id);

            await _appCollection.ReplaceOneAsync(filter, app);

        }

        /// <summary>
        /// Revisa que el usuario con ID userID tenga acceso a la app con ID appId. Para esto, busca el workspace donde se encuentra dicha app
        /// y confirma que tenga acceso a este. Si todo va bien, no ocurre nada; si hay algún percance, se lanza una excepción.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="appId"></param>
        /// <exception cref="ItemNotFoundException"></exception>
        /// <exception cref="NoAccessException"></exception>
        public async Task CanAccessApp(string userId, string appId)
        {
            var user = _context.Users.Include(u => u.Workspaces).FirstOrDefault(u => u.Id.ToString() == userId)
                ?? throw new ItemNotFoundException("User", "Id", userId);

            var app = GetAppById(appId)
                ?? throw new ItemNotFoundException("App", "Id", appId);

            WsAppsRelation wsAppRel = await _context.WsAppsRelations.FirstOrDefaultAsync(x => x.AppId == appId)
                ?? throw new ItemNotFoundException("Workspace-App Relation", "AppId", appId);

            if (!user.Workspaces.Any(ws => ws.Id == wsAppRel.WorkspaceId))
                throw new NoAccessException(userId, "Workspace", wsAppRel.WorkspaceId.ToString());
            
        }

        /// <summary>
        /// Revisa que el usuario con ID userId sea administrador del entorno del workspace con ID workspaceID, para confirmar si 
        /// puede crear aplicaciones en este.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="workspaceId"></param>
        /// <exception cref="NoAdminPermissionException"></exception>
        public async Task CanCreateApp(string userId, string workspaceId)
        {
            Workspace ws = await _workspaceServices.Value.GetWorkspaceById(workspaceId);

            if(! _context.UserToWorkEnvRoles.Any(x => x.WorkEnvironmentId == ws.WorkenvironmentId && x.UserId.ToString() == userId && x.IsAdmin))
                throw new NoAdminPermissionException(userId, "Work Environment", ws.WorkenvironmentId.ToString());
        }

        /// <summary>
        /// Revisa que el usuario con ID userId sea administrador del entorno de la app con ID appID, para confirmar si 
        /// puede modificar una aplicación en dicho entorno.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="appId"></param>
        /// <returns></returns>
        /// <exception cref="ItemNotFoundException"></exception>
        /// <exception cref="NoAccessException"></exception>
        /// <exception cref="NoAdminPermissionException"></exception>
        public async Task CanModifyApp(string userId, string appId)
        {
            WsAppsRelation wsAppRel = await _context.WsAppsRelations.FirstOrDefaultAsync(x => x.AppId == appId)
                ?? throw new ItemNotFoundException("Workspace-App Relation", "AppId", appId);

            var user = _context.Users.Include(u => u.Workspaces).FirstOrDefault(u => u.Id.ToString() == userId)
                ?? throw new ItemNotFoundException("User", "Id", userId);

            if (!user.Workspaces.Any(ws => ws.Id == wsAppRel.WorkspaceId))
                throw new NoAccessException(userId, "Workspace", wsAppRel.WorkspaceId.ToString());

            Workspace ws = await _workspaceServices.Value.GetWorkspaceById(wsAppRel.WorkspaceId.ToString());
            if(!_context.UserToWorkEnvRoles.Any(x => x.WorkEnvironmentId == ws.WorkenvironmentId && x.UserId.ToString() == userId && x.IsAdmin))
                throw new NoAdminPermissionException(userId, "Work Environment", ws.WorkenvironmentId.ToString());
        }
    }
}
