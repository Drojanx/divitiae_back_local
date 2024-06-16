using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using ZstdSharp.Unsafe;

namespace divitiae_api.Services
{
    public class WorkspaceServices : IWorkspaceServices
    {
        private readonly SQLDataContext _context;
        private readonly Lazy<IWorkEnvironmentServices> _workEnvironmentServices;
        private readonly Lazy<IWsAppsRelationServices> _wsAppsRelationServices;
        private readonly Lazy<IAppServices> _appServices;
        private readonly MongoClient _divitiaeClient;



        public WorkspaceServices(IOptions<MongoDBSettings> mongoDBSettings, SQLDataContext context, IHttpContextAccessor httpContentAccessor, Lazy<IWorkEnvironmentServices> workEnvironmentServices, Lazy<IAppServices> appServices, Lazy<IWsAppsRelationServices> wsAppsRelationServices)
        {
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            IMongoDatabase database = _divitiaeClient.GetDatabase(mongoDBSettings.Value.DatabaseName);
            _context = context;
            _workEnvironmentServices = workEnvironmentServices;
            _appServices = appServices;
            _wsAppsRelationServices = wsAppsRelationServices;
        }

        /// <summary>
        /// Elimina el workspace con ID id de la base de datos
        /// </summary>
        /// <param name="id"></param>
        public async Task DeleteWorkspace(string id)
        {
            var ws = await GetWorkspaceById(id);

            foreach(WsAppsRelation rel in ws.WsAppsRelations)
            {
                await _appServices.Value.DeleteApp(rel.AppId);
            }
            _context.Workspaces.Remove(ws);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Devuelve una lista con todos los workspaces de la base de datos
        /// </summary>
        /// <returns>Lista de Workspace</returns>
        public async Task<List<Workspace>> GetAllWorkspaces()
        {
            return await _context.Workspaces.ToListAsync();
        }

        /// <summary>
        /// Devuelve el workspace con ID id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>Workspace</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<Workspace> GetWorkspaceById(string id)
        {
            return await _context.Workspaces.Include(ws => ws.WsAppsRelations).FirstOrDefaultAsync(x => x.Id.ToString() == id)
                ?? throw new ItemNotFoundException("Workspace", "Id", id);
        }

        /// <summary>
        /// Devuelve un workspaceDTO basado en el workspace con ID id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>WorkspaceDTO</returns>
        public async Task<WorkspaceDTO> GetWorkspaceDTO(string id)
        {
            Workspace workspace = await GetWorkspaceById(id);
            
            var apps = new List<AppDTO>();
            foreach (var app in workspace.WsAppsRelations)
            {
                apps.Add(await _appServices.Value.GetAppDTO(app.AppId));
            }

            WorkspaceDTO workspaceDTO = new WorkspaceDTO
            {
                Id = workspace.Id.ToString(),
                WorkspaceName = workspace.WorkspaceName,
                Apps = apps
            };

            return workspaceDTO;
        }

        /// <summary>
        /// Inserta el workspace que recibe como argumento en base de datos
        /// </summary>
        /// <param name="workspace"></param>
        public async Task InsertWorkspace(Workspace workspace)
        {
            await _context.Workspaces.AddAsync(workspace);
            await _context.SaveChangesAsync();
        }

        //public async Task InsertWorkspace(Workspace workspace, string environmentId)
        //{
        //    if (_httpContentAccessor.HttpContext != null)
        //    {
        //        await _context.Workspaces.AddAsync(workspace);
        //        workspace.StringId = workspace.Id.ToString();
        //        await UpdateWorkspace(workspace);
        //        await AddWorkspaceToEnvironment(workspace, environmentId);
        //        await _context.SaveChangesAsync();

        //    }
        //}

        /// <summary>
        /// Busca el workspace que recibe como argumento y lo actualiza en base de datos
        /// </summary>
        /// <param name="workspace"></param>
        public async Task UpdateWorkspace(Workspace workspace)
        {
            var dbWs = await GetWorkspaceById(workspace.Id.ToString());
            dbWs.WorkspaceName = workspace.WorkspaceName;
            dbWs.WsAppsRelations = workspace.WsAppsRelations;
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Añade el workspace que recibe como argumento en el workEnvironment
        /// con ID environmentId y lo actualiza en base de datos
        /// </summary>
        /// <param name="workspace"></param>
        /// <param name="environmentId"></param>
        public async Task AddWorkspaceToEnvironment(Workspace workspace, string environmentId)
        {
            WorkEnvironment environment = await _workEnvironmentServices.Value.GetEnvironmentById(environmentId);
            environment.Workspaces.Add(workspace);
            await _workEnvironmentServices.Value.UpdateEnvironment(environment);
            await _context.SaveChangesAsync();
        }



        //public async Task<Workspace> InsertWelcomeWorkspace(Workspace workspace)
        //{
        //    await InsertWorkspace(workspace);    
            
        //    return workspace;

        //}

        /// <summary>
        /// Revisa si el usuario con ID userID tiene permiso de administrador en el
        /// workEnvironment con ID workEnvironmentID, para así confirmar si puede
        /// o no crear un workspace en dihco workEnvironment. Si no tuviese 
        /// permisos, se lanzaría una excepción
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="workEnvironmentId"></param>
        /// <exception cref="NoAdminPermissionException"></exception>
        public async Task CanCreateWS(string userId, string workEnvironmentId)
        {
            WorkEnvironment we = await _workEnvironmentServices.Value.GetEnvironmentById(workEnvironmentId);

            if (!_context.UserToWorkEnvRoles.Any(uTWER => uTWER.UserId.ToString() == userId && uTWER.WorkEnvironmentId.ToString() == workEnvironmentId && uTWER.IsAdmin))
                throw new NoAdminPermissionException(userId, "WorkEnvironment", workEnvironmentId);

        }

        /// <summary>
        /// Revisa si el usuario con ID userID tiene tiene acceso al workspace
        /// con ID workspaceID. Si no tuviese acceso, se lanzaría una excepción 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="workspaceId"></param>
        /// <returns></returns>
        /// <exception cref="NoAccessException"></exception>
        public async Task<WorkspaceDTO> CanAccessWS(string userId, string workspaceId)
        {
            var wsp = await GetWorkspaceDTO(workspaceId);
            var user = _context.Users.Include(u => u.Workspaces).FirstOrDefault(u => u.Id.ToString() == userId);
            if (!user.Workspaces.Any(ws => ws.Id.ToString() == workspaceId))
                throw new NoAccessException(user.Email, "Workspace", wsp.WorkspaceName);
            return wsp;
        }

        /// <summary>
        /// Revisa si el usuario con ID userID tiene acceso al workspace con ID
        /// workspaceID y, además, si tiene permisos de administrador en el 
        /// workEnvironment en que se encuentra. Si no tuviese permisos / acceso,
        /// se lanzaría una excepción
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="workspaceId"></param>
        /// <exception cref="ItemNotFoundException"></exception>
        /// <exception cref="NoAccessException"></exception>
        /// <exception cref="NoAdminPermissionException"></exception>
        public async Task CanModifyWS(string userId, string workspaceId)
        {
            await GetWorkspaceById(workspaceId);
            var user = _context.Users.Include(u =>u.Workspaces).FirstOrDefault(u => u.Id.ToString() == userId)
                ?? throw new ItemNotFoundException("User", "Id", userId);
            if (!user.Workspaces.Any(ws => ws.Id.ToString() == workspaceId))
                throw new NoAccessException(userId, "Workspace", workspaceId);
            Workspace ws = await GetWorkspaceById(workspaceId);
            if (!_context.UserToWorkEnvRoles.Any(x => x.WorkEnvironmentId == ws.WorkenvironmentId && x.UserId.ToString() == userId && x.IsAdmin))
                throw new NoAdminPermissionException(userId, "Work Environment", ws.WorkenvironmentId.ToString());
        }

    }
}
