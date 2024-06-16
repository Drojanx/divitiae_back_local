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
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http.HttpResults;

namespace divitiae_api.Services
{
    public class AppTaskServices : IAppTaskServices
    {
        private readonly IMongoCollection<AppTask> _taskCollection;
        private readonly IMongoCollection<BsonDocument> _genericItemCollection;
        private readonly IMongoDatabase _divitiaeDatabase;
        private readonly MongoClient _divitiaeClient ;
        private readonly Lazy<IAppServices> _appServices;
        private readonly IWorkEnvironmentServices _workEnvironmentServices;
        private readonly IUserToWorkEnvRoleServices _userToWorkEnvRoleServices;
        private readonly IUserServices _userServices;
        private readonly IWorkspaceServices _workspaceServices;
        private readonly IItemServices _itemServices;

        public AppTaskServices(IOptions<MongoDBSettings> mongoDBSettings, Lazy<IAppServices> appServices, IWorkEnvironmentServices workEnvironmentServices, IUserServices userServices, IWorkspaceServices workspaceServices, IItemServices itemServices, IUserToWorkEnvRoleServices userToWorkEnvRoleServices)
        {
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            IMongoDatabase database = _divitiaeClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
            _divitiaeDatabase = database;
            _taskCollection = database.GetCollection<AppTask>("Tasks");
            _genericItemCollection = database.GetCollection<BsonDocument>("Tasks");
            _appServices = appServices;
            _workEnvironmentServices = workEnvironmentServices;
            _userServices = userServices;
            _workspaceServices = workspaceServices;
            _itemServices = itemServices;
            _userToWorkEnvRoleServices = userToWorkEnvRoleServices;
        }

        /// <summary>
        /// Borra una appTask de la base de datos según su ID usando una sesión de MongoDB
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="session"></param>
        public async Task DeleteAppTask(string taskId, IClientSessionHandle session)
        {
            var filter = Builders<AppTask>.Filter.Eq(s => s.Id, new ObjectId(taskId));
            await _taskCollection.DeleteOneAsync(session, filter);
        }

        /// <summary>
        /// Crea una sesión de MongoDB y llama al método anterior, que borrará una appTask con ID taskID
        /// </summary>
        /// <param name="taskId"></param>
        public async Task DeleteAppTask(string taskId)
        {
            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {

                    var filter = Builders<AppTask>.Filter.Eq(s => s.Id, new ObjectId(taskId));
                    await _taskCollection.DeleteOneAsync(session, filter);
                }
                catch (Exception)
                {
                    throw;
                }

            }
        }

        /// <summary>
        /// Devuelve una appTask según su ID
        /// </summary>
        /// <param name="taskId"></param>
        /// <returns>AppTask</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<AppTask> GetAppTaskById(string taskId)
        {
            return await _taskCollection.Find(x => x.Id.ToString() == taskId).FirstOrDefaultAsync()
                ?? throw new ItemNotFoundException("AppTask", "Id", taskId);
        }

        /// <summary>
        /// Devuelve una lista de todas las AppTask del usuario con ID userID
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>Lista de AppTask</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<List<AppTask>> GetAppTasksByUser(string userId)
        {
            var createdByFilter = Builders<AppTask>.Filter.Eq(task => task.CreatedBy.Id, userId);
            var assignedUserFilter = Builders<AppTask>.Filter.Eq(task => task.AssignedUser.Id, userId);

            var combinedFilter = Builders<AppTask>.Filter.Or(assignedUserFilter, createdByFilter);

            return await _taskCollection.Find(combinedFilter).ToListAsync()
                ?? throw new ItemNotFoundException("AppTask", "User", userId);
        }

        /// <summary>
        /// Devuelve una lista de todas las AppTask del usuario con ID userID que pertenezcan al environment con
        /// ID environmentID
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="environmentId"></param>
        /// <returns>Lista de AppTask</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<List<AppTask>> GetAppTaskByUserAndEnvironment(string userId, string environmentId)
        {
            var userFilter = Builders<AppTask>.Filter.Eq(task => task.AssignedUser.Id, userId);
            var environmentFilter = Builders<AppTask>.Filter.Eq(task => task.Environment.Id, environmentId);

            var combinedFilter = Builders<AppTask>.Filter.And(userFilter, environmentFilter);


            return await _taskCollection.Find(combinedFilter).ToListAsync()
                ?? throw new ItemNotFoundException("AppTask", "User and Environment", userId+" and "+environmentId);
        }

        /// <summary>
        /// Crea una sesión de MongoDB e inserta usando esta una appTask en base de datos
        /// </summary>
        /// <param name="task"></param>
        /// <returns>AppTask</returns>
        public async Task<AppTask> InsertAppTask(AppTask task)
        {
            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    await _taskCollection.InsertOneAsync(session, task);


                    return task;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// Actualiza una appTask en base de datos
        /// </summary>
        /// <param name="task"></param>
        public async Task UpdateAppTask(AppTask task)
        {
            var filter = Builders<AppTask>
                .Filter
                .Eq(s => s.Id, task.Id);

            await _taskCollection.ReplaceOneAsync(filter, task);
        }

        /// <summary>
        /// Mapea un AppTaskDTOCreate a un AppTask básico y lo devuelve
        /// </summary>
        /// <param name="user"></param>
        /// <param name="taskDTO"></param>
        /// <returns>AppTask</returns>
        /// <exception cref="CustomBadRequestException"></exception>
        public async Task<AppTask> mapAppTaskDTOCreate(User user, AppTaskDTOCreate taskDTO)
        {
            WorkEnvironmentDTO weData = await _userToWorkEnvRoleServices.CanAccessWorkEnvironment(user.Id.ToString(), taskDTO.EnvironmentId);
            TaskUserDTO creator = new TaskUserDTO()
            {
                Id = user.Id.ToString(),
                Name = user.Name,
                LastName = user.LastName,
                Email = user.Email
            };
            TaskEnvironmentDTO taskEnvironmentDTO = new TaskEnvironmentDTO()
            {
                Id = weData.Id.ToString(),
                EnvironmenteName = weData.EnvironmentName
            };
            TaskWorkspaceDTO taskWorkspaceDTO = null;
            TaskAppDTO taskAppDTO = null;
            TaskItemDTO taskItemDTO = null;

            await _userToWorkEnvRoleServices.CanAccessWorkEnvironment(taskDTO.AssignedUserId, taskDTO.EnvironmentId);
            User assignedUser = await _userServices.GetUserById(taskDTO.AssignedUserId);
            TaskUserDTO taskUserDTO = new TaskUserDTO()
            {
                Id = taskDTO.AssignedUserId,
                Name = assignedUser.Name,
                LastName = assignedUser.LastName,
                Email = assignedUser.Email
            };


            if (taskDTO.WorkspaceId != null)
            {
                WorkspaceDTO wsData = await _workspaceServices.CanAccessWS(user.Id.ToString(), taskDTO.WorkspaceId);

                await _workspaceServices.CanAccessWS(taskDTO.AssignedUserId, taskDTO.WorkspaceId);

                taskWorkspaceDTO = new TaskWorkspaceDTO();
                taskWorkspaceDTO.Id = taskDTO.WorkspaceId;
                taskWorkspaceDTO.WorkspaceName = wsData.WorkspaceName;

                if (taskDTO.AppId != null)
                {
                    AppDTO app = wsData.Apps.Find(obj => obj.Id == taskDTO.AppId);
                    if (app == null)
                    {
                        throw new CustomBadRequestException("App with id " + taskDTO.AppId + " not found in Workspace with " + taskDTO.WorkspaceId + " id.");
                    }
                    taskAppDTO = new TaskAppDTO();
                    taskAppDTO.Id = app.Id;
                    taskAppDTO.AppName = app.AppName;

                    if (taskDTO.ItemId != null)
                    {
                        Item item = await _itemServices.GetAppItemById(taskDTO.ItemId, taskDTO.AppId);

                        taskItemDTO = new TaskItemDTO();
                        taskItemDTO.Id = item.Id.ToString();
                        taskItemDTO.DescriptiveName = item.DescriptiveName;
                    }
                }
                else if (taskDTO.ItemId != null)
                {
                    throw new CustomBadRequestException("To assign an item to the task, you need to specify its app aswell.");
                }
            }
            else if (taskDTO.AppId != null || taskDTO.ItemId != null)
            {
                throw new CustomBadRequestException("You can't assign an App or an Item to the task without assigning its Workspace first.");
            }

            // Obtener la fecha y hora actual
            DateTime now = DateTime.Now;
            // Redondear al minuto más cercano eliminando los segundos y milisegundos
            DateTime roundedToMinute = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            // Convertir a timestamp Unix (segundos desde 1970-01-01 00:00:00 UTC)
            DateTimeOffset dateTimeOffset = new DateTimeOffset(roundedToMinute);
            long unixTimestamp = dateTimeOffset.ToUnixTimeSeconds();

            AppTask appTask = new AppTask()
            {
                CreatedBy = creator,
                CreatedOn = unixTimestamp,
                Environment = taskEnvironmentDTO,
                AssignedUser = taskUserDTO,
                Workspace = taskWorkspaceDTO,
                App = taskAppDTO,
                Item = taskItemDTO,
                DueDate = taskDTO.DueDate,
                Information = taskDTO.Information,
                Finished = taskDTO.Finished
            };

            return appTask;
        }

        public async Task<List<AppTask>> GetAppTasksByItem(string itemId)
        {
            var itemFilter = Builders<AppTask>.Filter.Eq(task => task.Item.Id, itemId);

            return await _taskCollection.Find(itemFilter).ToListAsync()
                ?? throw new ItemNotFoundException("AppTask", "ItemId", itemId);
        }

        public async Task<List<AppTask>> GetAppTasksByApp(string appId)
        {
            var itemFilter = Builders<AppTask>.Filter.Eq(task => task.App.Id, appId);

            return await _taskCollection.Find(itemFilter).ToListAsync()
                ?? throw new ItemNotFoundException("AppTask", "AppId", appId);
        }

        public async Task<List<AppTask>> GetAppTasksByWorkspace(string workspaceId)
        {
            var itemFilter = Builders<AppTask>.Filter.Eq(task => task.Workspace.Id, workspaceId);

            return await _taskCollection.Find(itemFilter).ToListAsync()
                ?? throw new ItemNotFoundException("AppTask", "WorkspaceId", workspaceId);
        }
    }
}
