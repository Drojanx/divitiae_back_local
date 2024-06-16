using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualBasic;
using MongoDB.Driver;
using System;
using System.Dynamic;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace divitiae_api.Services
{
    public class WsAppsRelationServices : IWsAppsRelationServices
    {

        private readonly SQLDataContext _context;
        private readonly IHttpContextAccessor _httpContentAccessor;
        private readonly Lazy<IWorkEnvironmentServices> _workEnvironmentServices;
        private readonly Lazy<IWorkspaceServices> _workspaceServices;
        private readonly Lazy<IItemServices> _itemServices;


        public WsAppsRelationServices(SQLDataContext context, IHttpContextAccessor httpContentAccessor, Lazy<IWorkEnvironmentServices> workEnvironmentServices, Lazy<IWorkspaceServices> workspaceServices, Lazy<IItemServices> itemServices)
        {
            _context = context;
            _httpContentAccessor = httpContentAccessor;
            _workspaceServices = workspaceServices;
            _workEnvironmentServices = workEnvironmentServices;
            _itemServices = itemServices;
        }

        /// <summary>
        /// Elimina la entidad de relación entre el workspace con ID wsId y
        /// la app con ID appId de la base de datos
        /// </summary>
        /// <param name="wsId"></param>
        /// <param name="appId"></param>
        public async Task DeleteWsAppsRelation(string wsId, string appId)
        {
            WsAppsRelation wsAppsRelation = await GetWsAppsRelationByWsAndAppId(wsId, appId);
            _context.WsAppsRelations.Remove(wsAppsRelation);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Devuelve la relación, si la hay, entre el workspace con ID wsID y
        /// la app con ID appId
        /// </summary>
        /// <param name="wsId"></param>
        /// <param name="appId"></param>
        /// <returns>WsAppsRelation</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<WsAppsRelation> GetWsAppsRelationByWsAndAppId(string wsId, string appId)
        {
            return await _context.WsAppsRelations.FirstOrDefaultAsync(x => x.WorkspaceId.ToString() == wsId && x.AppId.ToString() == appId)
                ?? throw new ItemNotFoundException("Workspace-App relation", "Workspace and App Ids", $"{wsId} and {appId}");
        }

        public async Task InsertWsAppsRelation(WsAppsRelation wsAppsRelation)
        {
            //await _context.WsAppsRelations.AddAsync(wsAppsRelation);
            //await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Revisa si el usuario con ID userId tiene acceso al workEnvironment con ID weId. Si no
        /// lo tuviese, se lanzaría una excepción
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="weId"></param>
        /// <returns></returns>
        /// <exception cref="NoAccessException"></exception>
        public async Task<WorkEnvironmentDTO> CanAccessWorkEnvironment(string userId, string weId)
        {
            WorkEnvironmentDTO we = await _workEnvironmentServices.Value.GetEnvironmentDTO(userId, weId);
            UserToWorkEnvRole uTWERole = await _context.UserToWorkEnvRoles.FirstOrDefaultAsync(x => x.WorkEnvironmentId.ToString() == weId && x.UserId.ToString() == userId)
                ?? throw new NoAccessException(userId, "Work Environment", weId);
            return we;
        }
    }
}
