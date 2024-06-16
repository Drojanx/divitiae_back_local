using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.EntityFrameworkCore;
using MongoDB.Driver;
using System.Dynamic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;


namespace divitiae_api.Services
{
    public class UserServices : IUserServices
    {

        private readonly SQLDataContext _context;
        private readonly IHttpContextAccessor _httpContentAccessor;
        private readonly Lazy<IWorkEnvironmentServices> _workEnvironmentServices;
        private readonly Lazy<IWorkspaceServices> _workspaceServices;
        private readonly Lazy<IItemServices> _itemServices;
        private readonly Lazy<IAppServices> _appServices;
        private readonly Lazy<IUserToWorkEnvRoleServices> _userToWorkEnvRoleServices;

        public UserServices(SQLDataContext context, IHttpContextAccessor httpContentAccessor, Lazy<IWorkEnvironmentServices> workEnvironmentServices, Lazy<IWorkspaceServices> workspaceServices, Lazy<IItemServices> itemServices, Lazy<IAppServices> appServices, Lazy<IUserToWorkEnvRoleServices> userToWorkEnvRoleServices)
        {
            _context = context;
            _httpContentAccessor = httpContentAccessor;
            _workspaceServices = workspaceServices;
            _workEnvironmentServices = workEnvironmentServices;
            _itemServices = itemServices;
            _appServices = appServices;
            _userToWorkEnvRoleServices = userToWorkEnvRoleServices;
        }

        /// <summary>
        /// Elimina el user con ID stringId de la base de datos
        /// </summary>
        /// <param name="stringId"></param>
        public async Task DeleteUser(string stringId)
        {
            var user = await GetUserById(stringId);
            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Devuelve una lista de todos los user en la base de datos
        /// </summary>
        /// <returns>Lista de user</returns>
        public async Task<List<User>> GetAllUsers()
        {
            return await _context.Users.ToListAsync();
        }

        /// <summary>
        /// Devuelve un userDataDTO basado en el user que recibe como argumento, incluyendo sus
        /// environments, roles en estos, y workspaces
        /// </summary>
        /// <param name="user"></param>
        /// <returns>UserDataDTO</returns>
        /// <exception cref="Exception"></exception>
        public async Task<UserDataDTO> GetCurrentUserWithWorkEnvironmentsAndWorkspaces(User user)
        {
            var dbUser = _context.Users
                .Include(u => u.UserToWorkEnvRole)
                .ThenInclude(uwr => uwr.WorkEnvironment)
                .Where(u => u.Id == user.Id)
                .SingleOrDefault();
            if (dbUser == null)
                throw new Exception("User not found");
            var accessibleWorkspaceIds = dbUser.Workspaces.Select(uWs => uWs.Id).ToList();
            Console.WriteLine(dbUser);

            var userDataDTO = new UserDataDTO
            {
                UserId = dbUser.Id,
                UserName = dbUser.Name,
                UserLastName = dbUser.LastName,
                UserEmail = dbUser.Email,
                WorkEnvironments = dbUser.UserToWorkEnvRole.Select(uwr => new UserWorkEnvironmentDataDTO
                {
                    Id = uwr.WorkEnvironment.Id,
                    EnvironmentName = uwr.WorkEnvironment.EnvironmentName,
                    IsAdmin = uwr.IsAdmin,
                    IsOwner = uwr.IsOwner,
                    Workspaces = _context.Workspaces
                        .Where(w => w.WorkenvironmentId == uwr.WorkEnvironment.Id && accessibleWorkspaceIds.Contains(w.Id))
                        .Select( ws => ws.Id)
                        .ToList() 
                }).ToList()
            };            
                
            return userDataDTO;
        }


        /// <summary>
        /// Devuelve un userDataDTO basado en el user que recibe como argumento, incluyendo su rol en el
        /// environment que recibe como argumento
        /// </summary>
        /// <param name="user"></param>
        /// <param name="we"></param>
        /// <returns>UserDataDTO</returns>
        /// <exception cref="Exception"></exception>
        public UserDataDTO GetUserPermissionsOnWorkEnvironment(User user, WorkEnvironment we)
        {
            var dbUser = _context.Users
                .Where(u => u.Id == user.Id &&
                            u.UserToWorkEnvRole.Any(uwr => uwr.WorkEnvironmentId == we.Id))
                .Include(u => u.Workspaces)
                .Include(u => u.UserToWorkEnvRole)
                    .ThenInclude(uwr => uwr.WorkEnvironment)
                .SingleOrDefault();

            if (dbUser == null)
                throw new Exception("User not found");

            if (dbUser != null)
            {
                dbUser.UserToWorkEnvRole = dbUser.UserToWorkEnvRole
                    .Where(uwr => uwr.WorkEnvironment == we)
                    .ToList();
            }

            var accessibleWorkspaceIds = dbUser.Workspaces.Select(uWs => uWs.Id).ToList();
            Console.WriteLine(dbUser);

            var userDataDTO = new UserDataDTO
            {
                UserId = dbUser.Id,
                UserName = dbUser.Name,
                UserLastName = dbUser.LastName,
                UserEmail = dbUser.Email,
                WorkEnvironments = dbUser.UserToWorkEnvRole.Select(uwr => new UserWorkEnvironmentDataDTO
                {
                    Id = uwr.WorkEnvironment.Id,
                    EnvironmentName = uwr.WorkEnvironment.EnvironmentName,
                    IsAdmin = uwr.IsAdmin,
                    IsOwner = uwr.IsOwner,
                    Workspaces = _context.Workspaces
                        .Where(w => w.WorkenvironmentId == uwr.WorkEnvironment.Id && accessibleWorkspaceIds.Contains(w.Id))
                        .Select( ws => ws.Id)
                        .ToList()                        
                }).ToList()
            };
        
            return userDataDTO;
        }

        /// <summary>
        /// Devuelve el user cuyo email coincida con el string email que recibe como argumento
        /// </summary>
        /// <param name="email"></param>
        /// <returns>User</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<User> GetUserByEmail(string email)
        {
            return await _context.Users.Include(u => u.Workspaces).Include(u => u.UserToWorkEnvRole).FirstOrDefaultAsync(u => u.Email == email)
                ?? throw new ItemNotFoundException("user", "email", email);
        }

        /// <summary>
        /// Revisa si existe algún user cuyo email sea igual al string email que recibe como argumento
        /// </summary>
        /// <param name="email"></param>
        /// <returns>bool</returns>
        public async Task<bool> UserByEmailExists(string email)
        {
            return await _context.Users.Include(u => u.Workspaces).Include(u => u.UserToWorkEnvRole).FirstOrDefaultAsync(u => u.Email == email) != null;
        }


        //public async Task<User> GetUserByGoogleId(string id)
        //{
        //    return await _context.Users.FirstOrDefaultAsync(x => x.GoogleUID == id);
        //}

        /// <summary>
        /// Devuelve el user con ID id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>User</returns>
        /// <exception cref="ItemNotFoundException"></exception>
        public async Task<User> GetUserById(string id)
        {
            User user = await _context.Users.Include(u => u.Workspaces).Include(u => u.UserToWorkEnvRole).FirstOrDefaultAsync(x => x.Id.ToString() == id);
            return user ?? throw new ItemNotFoundException("User", "Id", id);

        }

        public string HashPassword(string password)
        {
            SHA256 hash = SHA256.Create();

            var passwordBytes = Encoding.Default.GetBytes(password);

            var hashedPassword = hash.ComputeHash(passwordBytes);

            return Convert.ToHexString(hashedPassword);
        }

        /// <summary>
        /// Inserta el user que recibe como argumento en base de datos
        /// </summary>
        /// <param name="user"></param>
        /// <returns>User</returns>
        public async Task<User> InsertUser(User user)
        {
            try
            {
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
                return user;
            }
            catch (Exception)
            {

                throw; 
            }

        }

        /// <summary>
        /// Actualiza el user que recibe como argumento en base de datos
        /// </summary>
        /// <param name="user"></param>
        public async Task UpdateUser(User user)
        {
            var dbUser = await GetUserById(user.Id.ToString());

            if (dbUser != null)
            {
                dbUser.Email = user.Email;
                dbUser.Name = user.Name;
                dbUser.LastName = user.LastName;
                dbUser.UserToWorkEnvRole = user.UserToWorkEnvRole;
                dbUser.Workspaces = user.Workspaces;

                await _context.SaveChangesAsync();
            }
        }

    }
}
