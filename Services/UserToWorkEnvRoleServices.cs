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
    public class UserToWorkEnvRoleServices : IUserToWorkEnvRoleServices
    {

        private readonly SQLDataContext _context;
        private readonly IHttpContextAccessor _httpContentAccessor;
        private readonly Lazy<IWorkEnvironmentServices> _workEnvironmentServices;
        private readonly Lazy<IWorkspaceServices> _workspaceServices;
        private readonly Lazy<IUserServices> _userServices;
        private readonly Lazy<IItemServices> _itemServices;


        public UserToWorkEnvRoleServices(Lazy<IUserServices> userServices, SQLDataContext context, IHttpContextAccessor httpContentAccessor, Lazy<IWorkEnvironmentServices> workEnvironmentServices, Lazy<IWorkspaceServices> workspaceServices, Lazy<IItemServices> itemServices)
        {
            _context = context;
            _httpContentAccessor = httpContentAccessor;
            _workspaceServices = workspaceServices;
            _workEnvironmentServices = workEnvironmentServices;
            _itemServices = itemServices;
            _userServices = userServices;
        }

        /// <summary>
        /// Revisa si existe alguna relación entre el user con ID userID y el environment con ID weID
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="weId"></param>
        /// <returns>bool</returns>
        public async Task<bool> UserToWorkEnvRoleExists(string userId, string weId)
        {
            var uToWERole = _context.UserToWorkEnvRoles.FirstOrDefault(x => x.UserId.ToString() == userId && x.WorkEnvironmentId.ToString() == weId);
            return (uToWERole != null);
        }

        /// <summary>
        /// Elimina la relación entre el user con ID userID y el environment con ID weID
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="weId"></param>
        /// <exception cref="ItemNotFoundException"></exception>
        /// <exception cref="BadHttpRequestException"></exception>
        public async Task DeleteUserToWorkEnvRole(string userId, string weId)
        {

            User user = await _userServices.Value.GetUserById(userId)
                ?? throw new ItemNotFoundException("User", "Id", userId);

            WorkEnvironment we = await _workEnvironmentServices.Value.GetEnvironmentById(weId)
                ?? throw new ItemNotFoundException("Work Environment", "Id", weId);

            var uToWERole = _context.UserToWorkEnvRoles.FirstOrDefault(x => x.UserId.ToString() == userId && x.WorkEnvironmentId.ToString() == weId)
                ?? throw new BadHttpRequestException($"User {userId} cannot be removed from Work Environment {weId} becouse it currently has no acces to it.");

            user.Workspaces = user.Workspaces.Except(we.Workspaces).ToList();

            await _userServices.Value.UpdateUser(user);
            _context.UserToWorkEnvRoles.Remove(uToWERole);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Inserta una relación entre un user y un environment en base de datos
        /// </summary>
        /// <param name="uToWERole"></param>
        public async Task InsertUserToWorkEnvRole(UserToWorkEnvRole uToWERole)
        {
            await _context.UserToWorkEnvRoles.AddAsync(uToWERole);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Devuelve todas las relaciones que el environment con ID workEnvId tenga con users de la aplicación
        /// </summary>
        /// <param name="workEnvId"></param>
        /// <returns>Lista de UserToWorkEnvRole</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<List<UserToWorkEnvRole>> GetAllUserToWorkEnvRoleByWorkEnvId(Guid workEnvId)
        {
            var roles = _context.UserToWorkEnvRoles.Where(role => role.WorkEnvironmentId == workEnvId).ToList()
                ?? throw new ItemNotFoundException("Role", "WorkEnvironment Id", workEnvId.ToString());
            return roles;
        }

        /// <summary>
        /// Devuelve todas las relaciones que el environment con ID workEnvId tenga con users de la aplicación
        /// </summary>
        /// <param name="workEnvId"></param>
        /// <returns>Lista de UserToWorkEnvRole</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<UserToWorkEnvRole> GetUserToWorkEnvRoleByUserIdAndWorkEnvId(string workEnvId, string userId)
        {
            var roles = _context.UserToWorkEnvRoles.Include(t => t.User).Where(role => role.WorkEnvironmentId.ToString() == workEnvId && role.User.Id.ToString() == userId).FirstOrDefault()
                ?? throw new ItemNotFoundException("Role", "User Id and WorkEnvironment Id", userId+" and "+workEnvId);
            return roles;
        }

        /// <summary>
        /// Revisa si el usuario con ID userId tiene permisos de administrador en el workEnvironment con ID weId y
        /// así poder modificarlo. Si no tuviese permisos, se lanzaría una excepción
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="weId"></param>
        /// <exception cref="ItemNotFoundException"></exception>
        /// <exception cref="NoAccessException"></exception>
        /// <exception cref="NoAdminPermissionException"></exception>
        public async Task CanModifyWorkEnvironment(string userId, string weId)
        {
            await _workEnvironmentServices.Value.GetEnvironmentById(weId);
            var user = _context.Users.Include(u => u.Workspaces).FirstOrDefault(u => u.Id.ToString() == userId)
                ?? throw new ItemNotFoundException("User", "Id", userId);

            UserToWorkEnvRole uTWERole = await _context.UserToWorkEnvRoles.FirstOrDefaultAsync(x => x.WorkEnvironmentId.ToString() == weId && x.UserId.ToString() == userId)
                ?? throw new NoAccessException(userId, "Work Environment", weId);
            if (!uTWERole.IsAdmin)
                throw new NoAdminPermissionException(userId, "Work Environment", weId);
        }

        public async Task<bool> UserIsOwner(string workEnvId, string userId)
        {
            var role = await _context.UserToWorkEnvRoles.FirstOrDefaultAsync(role => role.WorkEnvironmentId.ToString() == workEnvId && role.UserId.ToString() == userId)
                ?? throw new ItemNotFoundException("Role", "WorkEnvironment Id", workEnvId.ToString());

            return role.IsOwner;
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
            User user = await _userServices.Value.GetUserById(userId);
            WorkEnvironmentDTO we = await _workEnvironmentServices.Value.GetEnvironmentDTO(userId, weId);
            UserToWorkEnvRole uTWERole = await _context.UserToWorkEnvRoles.FirstOrDefaultAsync(x => x.WorkEnvironmentId.ToString() == weId && x.UserId.ToString() == userId)
                ?? throw new NoAccessException(user.Email, "Work Environment", we.EnvironmentName);
            return we;
        }

        public async Task<List<UserToWorkEnvRole>> GetEveryOwnerRole(string weId)
        {
            var roles = _context.UserToWorkEnvRoles.Where(role => role.WorkEnvironmentId.ToString() == weId && role.IsOwner).ToList();
            return roles;
        }
    }

    
}
