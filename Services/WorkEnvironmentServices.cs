using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics.Eventing.Reader;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace divitiae_api.Services
{
    public class WorkEnvironmentServices : IWorkEnvironmentServices
    {
        private readonly SQLDataContext _context;
        private readonly Lazy<IWorkspaceServices> _workspaceServices;
        private readonly Lazy<IUserServices> _userServices;
        private readonly Lazy<IUserToWorkEnvRoleServices> _userToWorkEnvRoleServices;

        public WorkEnvironmentServices(SQLDataContext context, IHttpContextAccessor httpContentAccessor, Lazy<IWorkspaceServices> workspaceServices, Lazy<IUserServices> userServices, Lazy<IUserToWorkEnvRoleServices> userToWorkEnvRoleServices)
        {
            _context = context;
            _workspaceServices = workspaceServices;
            _userServices = userServices;
            _userToWorkEnvRoleServices = userToWorkEnvRoleServices;
        }

        /// <summary>
        /// Elimina el workEnvironment con ID id de la base de datos
        /// </summary>
        /// <param name="id"></param>
        public async Task DeleteEnvironment(string id)
        {
            var we = await GetEnvironmentById(id);
            _context.WorkEnvironments.Remove(we);
            await _context.SaveChangesAsync();

        }

        /// <summary>
        /// Deuvelve una lista de todos los workEnvironments en base de datos
        /// </summary>
        /// <returns>Lista de WorkEnvironment</returns>
        public async Task<List<WorkEnvironment>> GetAllEnvironments()
        {
            return await _context.WorkEnvironments.ToListAsync();
        }

        /// <summary>
        /// Devuelve el workEnvironment con ID id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>WorkEnvironment</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<WorkEnvironment> GetEnvironmentById(string id)
        {
            return await _context.WorkEnvironments.Include(we => we.Workspaces).Include(we => we.UserToWorkEnvRole).FirstOrDefaultAsync(x => x.Id.ToString() == id)
                ?? throw new ItemNotFoundException("Work Environment", "Id", id);
        }

        /// <summary>
        /// Devuelve un environmentDTO basado en el workEnvironment con id ID. Este DTO trae también la información
        /// de los usuarios con acceso al mismo.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="id"></param>
        /// <returns>WorkEnvironment</returns>
        public async Task<WorkEnvironmentDTO> GetEnvironmentDTO(string userId, string id)
        {
            WorkEnvironment workEnvironment = await GetEnvironmentById(id);
            User user = await _userServices.Value.GetUserById(userId);
            var workspaces = new List<WorkspaceDTO>();

            var accesibleWs = workEnvironment.Workspaces.Intersect(user.Workspaces);
            foreach (var workspace in accesibleWs)
            {
                workspaces.Add(await _workspaceServices.Value.GetWorkspaceDTO(workspace.Id.ToString()));
            }

            var roles = await _userToWorkEnvRoleServices.Value.GetAllUserToWorkEnvRoleByWorkEnvId(workEnvironment.Id);

            List<WorkEnvironmentUserDataDTO> rolesDTO = new List<WorkEnvironmentUserDataDTO>();
            foreach (var  role in roles)
            {
                User weUser = await _userServices.Value.GetUserById(role.UserId.ToString());

                WorkEnvironmentUserDataDTO userDataDTO = new WorkEnvironmentUserDataDTO
                {
                    UserId = role.UserId,
                    UserEmail = weUser.Email,
                    UserDisplayName = weUser.Name + " " + weUser.LastName,
                    IsAdmin = role.IsAdmin,
                    IsOwner = role.IsOwner
                };

                rolesDTO.Add(userDataDTO);
            }

            WorkEnvironmentDTO workEnvironmentDTO = new WorkEnvironmentDTO
            {
                Id = workEnvironment.Id,
                EnvironmentName = workEnvironment.EnvironmentName,
                Workspaces = workspaces,
                UsersData = rolesDTO
            };

            return workEnvironmentDTO;
        }

        /// <summary>
        /// Inserta un workEnvironment en base de datos
        /// </summary>
        /// <param name="environment"></param>
        public async Task InsertEnvironment(WorkEnvironment environment)
        {
            await _context.WorkEnvironments.AddAsync(environment);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Busca el workEnvironment que recibe como argumento y lo actualiza en base de datos
        /// </summary>
        /// <param name="environment"></param>
        public async Task UpdateEnvironment(WorkEnvironment environment)
        {
            var dbWe = await GetEnvironmentById(environment.Id.ToString());

            dbWe.EnvironmentName = environment.EnvironmentName;
            dbWe.UserToWorkEnvRole = environment.UserToWorkEnvRole;
            dbWe.Workspaces = environment.Workspaces;

            await _context.SaveChangesAsync();
        }


        /// <summary>
        /// Llama al método anterior que inserta el workEnvironment we en base de datos
        /// </summary>
        /// <param name="we"></param>
        /// <param name="userName"></param>
        /// <returns></returns>
        //public async Task<WorkEnvironment> InsertWelcomeEnvironment(WorkEnvironment we)
        //{
        //    await InsertEnvironment(we);
        //    return we;
        //}

        /// <summary>
        /// Revisa si el usuario con ID userId tiene permisos de administrador en el workEnvironment con ID weId y
        /// así poder modificarlo. Si no tuviese permisos, se lanzaría una excepción
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="weId"></param>
        /// <exception cref="ItemNotFoundException"></exception>
        /// <exception cref="NoAccessException"></exception>
        /// <exception cref="NoAdminPermissionException"></exception>
        //public async Task CanModifyWorkEnvironment(string userId, string weId)
        //{
        //    await GetEnvironmentById(weId);
        //    var user = _context.Users.Include(u => u.Workspaces).FirstOrDefault(u => u.Id.ToString() == userId)
        //        ?? throw new ItemNotFoundException("User", "Id", userId);

        //    UserToWorkEnvRole uTWERole = await _context.UserToWorkEnvRoles.FirstOrDefaultAsync(x => x.WorkEnvironmentId.ToString() == weId && x.UserId.ToString() == userId)
        //        ?? throw new NoAccessException(userId, "Work Environment", weId);
        //    if (!uTWERole.IsAdmin)
        //        throw new NoAdminPermissionException(userId, "Work Environment", weId);
        //}

        /// <summary>
        /// Revisa si el usuario con ID userId tiene acceso al workEnvironment con ID weId. Si no
        /// lo tuviese, se lanzaría una excepción
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="weId"></param>
        /// <returns></returns>
        /// <exception cref="NoAccessException"></exception>
        //public async Task<WorkEnvironmentDTO> CanAccessWorkEnvironment(string userId, string weId)
        //{
        //    WorkEnvironmentDTO we = await GetEnvironmentDTO(userId, weId);
        //    UserToWorkEnvRole uTWERole = await _context.UserToWorkEnvRoles.FirstOrDefaultAsync(x => x.WorkEnvironmentId.ToString() == weId && x.UserId.ToString() == userId)
        //        ?? throw new NoAccessException(userId, "Work Environment", weId);
        //    return we;
        //}
    }
}
