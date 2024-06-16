using Amazon.Runtime.Internal;
using AutoMapper;
using Azure.Core;
using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Models.Mailing;
using divitiae_api.Services;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using Newtonsoft.Json.Bson;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;


namespace divitiae_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserController : ControllerBase
    {
        private readonly IEmailServices _emailServices;
        private readonly IUserServices _userServices;
        private readonly IWorkEnvironmentServices _workEnvironmentServices;
        private readonly IWorkspaceServices _workspaceServices;
        private readonly IAppServices _appServices;
        private readonly IUserToWorkEnvRoleServices _userToWorkEnvRoleServices;
        private readonly IWsAppsRelationServices _wsAppsRelationsServices;
        private readonly IAppTaskServices _appTaskServices;
        private readonly SQLDataContext _context;
        private readonly IHttpContextAccessor _httpContentAccessor;
        private readonly MongoClient _divitiaeClient;

        private readonly IConfiguration _configuration;
        private readonly IMapper _iMapper;

        public UserController(IOptions<MongoDBSettings> mongoDBSettings, IHttpContextAccessor httpContentAccessor, SQLDataContext context, IUserServices userServices, IWorkEnvironmentServices workEnvironmentServices, IWorkspaceServices workspaceServices, IAppServices appServices,IConfiguration configuration, IMapper iMapper, IUserToWorkEnvRoleServices userToWorkEnvRoleServices, IWsAppsRelationServices wsAppsRelationServices, IEmailServices emailServices, IAppTaskServices appTaskServices)
        {
            _userServices = userServices;
            _workEnvironmentServices = workEnvironmentServices;
            _workspaceServices = workspaceServices;
            _appServices = appServices;
            _configuration = configuration;
            _iMapper = iMapper;
            _context = context;
            _userToWorkEnvRoleServices = userToWorkEnvRoleServices;
            _wsAppsRelationsServices = wsAppsRelationServices;
            _httpContentAccessor = httpContentAccessor;
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            _emailServices = emailServices;
            _appTaskServices = appTaskServices;
        }

        /// <summary>
        /// Devuelve una lista de todos los usuarios en base de datos.
        /// </summary>
        /// <returns>Lista de User</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllUsers()
        {
            return Ok(await _userServices.GetAllUsers());
        }

        /// <summary>
        /// Devuelve el ID del usuario con email igual al recibido como argumento
        /// </summary>
        /// <param name="email"></param>
        /// <returns>Guid</returns>
        [HttpGet("by-email/{email}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserByEmail(string email)
        {
            try
            {
                User user = await _userServices.GetUserByEmail(email);
                return Ok(user.Id);
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Devuelve el usuario con ID id.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>User</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                User user = await _userServices.GetBasicUserById(id);
                return Ok(user);
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        /// <summary>
        /// Devuelve un userDataDTO del usuario actual.
        /// </summary>
        /// <returns>UserDataDTO</returns>
        [HttpGet("userdata")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetCurrentUserData()
        {

            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);
                UserDataDTO userData = await _userServices.GetCurrentUserWithWorkEnvironmentsAndWorkspaces(user);
                List<AppTask> tasks = await _appTaskServices.GetAppTasksByUser(user.Id.ToString());
                List<AppTaskDTO> tasksDTO = new List<AppTaskDTO>();
                foreach (AppTask task in tasks)
                {
                    AppTaskDTO taskDTO = new AppTaskDTO()
                    {
                        Id = task.Id.ToString(),
                        CreatedBy = task.CreatedBy,
                        CreatedOn = task.CreatedOn,
                        Environment = task.Environment,
                        AssignedUser = task.AssignedUser,
                        Workspace = task.Workspace,
                        App = task.App,
                        Item = task.Item,
                        DueDate = task.DueDate,
                        Information = task.Information,
                        Comments = task.Comments,
                        Finished = task.Finished
                    };
                    tasksDTO.Add(taskDTO);
                }
                userData.UserTasks = tasksDTO;
                return Ok(new { userData });
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (AggregateException ex)
            {
                return NotFound(ex.Message);
            }

            //UserDataDTO userDataDTO = _iMapper.Map<UserDataDTO>(userData);

        }

        /// <summary>
        /// Revisa que no exista un usuario con el mismo mail en base de datos. Si es
        /// correcto, hashea la contraseña del usuario e inserta su información en base de datos.
        /// Si hubiese marcado que quiere que se genere el environment de ejemplo, se genera también.
        /// </summary>
        /// <param name="request"></param>
        /// <returns>Created(201)</returns>
        [HttpPost("register"), AllowAnonymous]         
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> CreateUser([FromBody] UserDTORegister request)
        {
            using var transaction = _context.Database.BeginTransaction();

            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    if (request == null)
                        return BadRequest();

                    if (await _userServices.UserByEmailExists(request.Email))
                    {
                        return Conflict($"A user with the email {request.Email} already exists.");
                    }

                    User user = new User();

                    CreatePasswordHash(request.Password, out byte[] passwordHash, out byte[] passwordSalt);

                    user.Name = request.Name;
                    user.LastName = request.LastName;
                    user.Email = request.Email;
                    user.PasswordHash = passwordHash;
                    user.PasswordSalt = passwordSalt;
                    user.GoogleUID = String.Empty;

                    if (request.GenerateSampleEnvironment)
                    {
                        WorkEnvironment environment = new WorkEnvironment() { EnvironmentName = user.Name + "'s Environment" };

                        Workspace workspace = new Workspace()
                        {
                            WorkspaceName = "Welcome, " + user.Name,
                            Workenvironment = environment
                        };

                        UserToWorkEnvRole usrToWRRole = new UserToWorkEnvRole
                        {
                            User = user,
                            WorkEnvironment = environment,
                            IsAdmin = true,
                            IsOwner = true,
                        };

                        user.UserToWorkEnvRole.Add(usrToWRRole);
                        user.Workspaces.Add(workspace);
                        environment.UserToWorkEnvRole.Add(usrToWRRole);
                        workspace.Users.Add(user);

                        await _userServices.InsertUser(user);
                        await _context.SaveChangesAsync();


                        List<App> sampleApps = await _appServices.InsertWelcomeApps(session, workspace);

                        foreach (App app in sampleApps)
                        {
                            WsAppsRelation wsAppsRelation = new WsAppsRelation
                            {
                                AppId = app.Id.ToString(),
                                Workspace = workspace
                            };

                            workspace.WsAppsRelations.Add(wsAppsRelation);
                            app.WorkspaceId = workspace.Id.ToString();
                            await _appServices.UpdateApp(app);
                        }

                        environment.Workspaces.Add(workspace);
                        await _context.SaveChangesAsync();

                    }
                    else
                    {
                        await _userServices.InsertUser(user);
                        await _context.SaveChangesAsync();

                    }

                    transaction.Commit();

                    MailRequest mailRequest = new MailRequest();
                    mailRequest.ToEmail = user.Email;
                    mailRequest.Subject = "Welcome to Divitiae";
                    mailRequest.Body = BuildWelcomeHtmlContent(user.Name, user.Id.ToString());
                    await _emailServices.SendEmailAsync(mailRequest);
                    return Created("Created", true);

                }
                catch (ItemNotFoundException ex)
                {
                    transaction.Rollback();
                    return NotFound(ex.Message);
                }
                catch (DbUpdateException ex)
                {
                    transaction.Rollback();
                    if (ex.InnerException is SqlException sqlException &&
                       (sqlException.Number == 2601 || sqlException.Number == 2627))
                    {
                        return Conflict(sqlException.Message);
                    }
                    else
                    {
                        return StatusCode(500, "Unexpected error");
                    }

                }

            }            
        }

        /// <summary>
        /// Revisa si la información del userDTOLogin es correcta. Si es así, devuelve
        /// su token.
        /// </summary>
        /// <param name="request"></param>
        /// <returns></returns>
        [HttpPost("login"), AllowAnonymous]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> LogIn([FromBody] UserDTOLogin request)
        {
            try
            {
                if (request == null) { return BadRequest(); }

                User check = await _userServices.GetUserByEmail(request.Email);

                if (check == null)
                {
                    return NotFound("User not found.");
                }

                if (!VerifyPasswordHash(request.Password, check.PasswordHash, check.PasswordSalt))
                {
                    return BadRequest("Wrong password.");
                }
                string token = CreateToken(check);
                return Ok(new { token });
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            
        }

        private string CreateToken(User user)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.Email, user.Email)
            };

            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(
                _configuration.GetSection("AppSettings:Token").Value));

            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);

            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.Now.AddDays(60),
                signingCredentials: creds);

            var jwt = new JwtSecurityTokenHandler().WriteToken(token);

            return jwt;
        }

        /// <summary>
        /// Revisa que el usuario actual y el usuario a modificar son el mismo. Si es así,
        /// actualiza el usuario en base de datos.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateUser([FromBody] UserDTO request, string id)
        {
            if (request == null) { return BadRequest(); }

            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User currentUser = await _userServices.GetUserByEmail(result);
            User user = await _userServices.GetUserById(id);
            if (currentUser.Id == user.Id)
            {
                user.Name = request.Name;
                user.LastName = request.LastName;
                await _userServices.UpdateUser(user);

                return Created("Created", true);
            } else
            {
                return StatusCode(403, "You can only modify your own user information");
            }
        }

        /// <summary>
        /// Revisa que el usuario actual tiene permisos para modificar el environment. Si es así,
        /// actualiza los permisos de acceso a workspaces el usuario con ID id, según los permisos
        /// indicados en la lsita de workspacePermissionDTO
        /// </summary>
        /// <param name="request"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPut("{id}/update-workspaces/{envId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateUserWorkspaces([FromBody] List<WorkspacePermissionDTO> request, string id, string envId)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User loggedUser = await _userServices.GetUserByEmail(result);
            await _userToWorkEnvRoleServices.CanModifyWorkEnvironment(loggedUser.Id.ToString(), envId);

            if (request == null) { return BadRequest(); }

            User user = await _userServices.GetUserById(id);

            foreach (var workspace in user.Workspaces)
            {
                var newPermission = request.Find(x => x.Id == workspace.Id.ToString());

                if (newPermission == null) 
                {
                    continue; 
                }else if (!newPermission.Access)
                {
                    user.Workspaces.RemoveAll(x => x.Id == workspace.Id);
                }
            }

            foreach (var workspace in request)
            {
                var newPermission = user.Workspaces.Find(x => x.Id.ToString() == workspace.Id);

                if (newPermission == null)
                {
                    Workspace ws = await _workspaceServices.GetWorkspaceById(workspace.Id);
                    user.Workspaces.Add(ws);
                }
            }

            await _userServices.UpdateUser(user);

            return Created("Created", true);
        }

        /// <summary>
        /// Revisa que el usuario actual es el mismo usuario intentando ser eliminado.
        /// Si es así, lo elimina de base de datos.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>NoContent(204)</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteUser(string id)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User currentUser = await _userServices.GetUserByEmail(result);

            if (currentUser.Id.ToString() == id)
            {
                await _userServices.DeleteUser(id);

                return NoContent();

            }
            else
            {
                return StatusCode(403, "You can only delete your own user.");
            }

        }

        /// <summary>
        /// Revisa que el usuario actual tiene permisos de modificación en el entorno con ID envId. Si es así,
        /// devuelve el userDataDTO del usuario con ID id.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="envId"></param>
        /// <returns>UserDataDTO</returns>
        [HttpGet("permissions/{id}/environment/{envId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetUserEnvironmentPermissions(string id, string envId)
        {

            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User loggedUser = await _userServices.GetUserByEmail(result);
                await _userToWorkEnvRoleServices.CanModifyWorkEnvironment(loggedUser.Id.ToString(), envId);
                WorkEnvironment we = await _workEnvironmentServices.GetEnvironmentById(envId);
                User user = await _userServices.GetUserById(id);

                UserDataDTO userData = _userServices.GetUserPermissionsOnWorkEnvironment(user, we);
                return Ok(new { userData });
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (NoAdminPermissionException ex)
            {
                return StatusCode(403, ex.Message);
            }


        }

        private void CreatePasswordHash(string password, out byte[] passwordHash, out byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512())
            {
                passwordSalt = hmac.Key;
                passwordHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));

            }
        }

        private bool VerifyPasswordHash(string password, byte[] passwordHash, byte[] passwordSalt)
        {
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                var computedHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                return computedHash.SequenceEqual(passwordHash);
            }
        }

        private async void WelcomePack(User user, IClientSessionHandle session)
        {
            WorkEnvironment environment = new WorkEnvironment() { EnvironmentName = user.Name + "'s Environment" };

            Workspace workspace = new Workspace()
            {
                WorkspaceName = "Welcome, " + user.Name,
                Workenvironment = environment
            };

            UserToWorkEnvRole usrToWRRole = new UserToWorkEnvRole
            {
                User = user,
                WorkEnvironment = environment,
                IsAdmin = true
            };

            user.UserToWorkEnvRole.Add(usrToWRRole);
            user.Workspaces.Add(workspace);
            environment.UserToWorkEnvRole.Add(usrToWRRole);
            workspace.Users.Add(user);


            List<App> sampleApps = await _appServices.InsertWelcomeApps(session, workspace);

            foreach (App app in sampleApps)
            {
                WsAppsRelation wsAppsRelation = new WsAppsRelation
                {
                    AppId = app.Id.ToString(),
                    Workspace = workspace
                };

                workspace.WsAppsRelations.Add(wsAppsRelation);
                app.WorkspaceId = workspace.Id.ToString();
                await _appServices.UpdateApp(app);
            }

            environment.Workspaces.Add(workspace);
        }

        private string BuildWelcomeHtmlContent(string name, string id)
        {
            string content = "<table style=\"padding: 30px; background-color: #00E18A; border-radius: 20px;\" class=\"es-wrapper\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tbody><tr><td class=\"esd-email-paddings\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"es-content\" align=\"center\"><tbody><tr><td class=\"esd-stripe\" align=\"center\"><table style=\"padding: 20px; border-radius: 20px; background-color: white;font-family:'Gordita',Helvetica,Arial,sans-serif!important\" class=\"es-content-body\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\" width=\"600\"><tbody><tr><td class=\"esd-structure es-p15t es-p30b es-p20r es-p20l\" align=\"left\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td width=\"560\" class=\"esd-container-frame\" align=\"center\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td align=\"center\" class=\"esd-block-image es-p10t es-p10b\" style=\"font-size: 0px;\"><a target=\"_blank\"><img src=\"https://fiigjir.stripocdn.email/content/guids/CABINET_fc3ee6f268148614767e499f231f5b5bb667bc3ba605dd1e448c87775b6cafc0/images/divitiaecolor.png\" alt=\"\" style=\"display:block\" width=\"325\" class=\"adapt-img\"></a></td></tr><tr><td align=\"center\" class=\"esd-block-text es-p10t es-p10b es-p40r es-p40l es-m-p0r es-m-p0l\"><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Hello, " + name + "!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Thanks for joining us!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Start managing your project your way!</p></td></tr><tr><td align=\"center\" class=\"esd-block-button es-p5t es-p5b\"><span class=\"es-button-border\" style=\"font-family:'Gordita',Helvetica,Arial,sans-serif!important\"><a href=\"http://localhost:8080/login\" class=\"es-button es-button-4663\" target=\"_blank\" style=\"text-decoration: none; padding: 10px 30px 10px 30px; border-radius: 20px; font-size: 20px; font-weight: bold; background:#00E18A;mso-border-alt:10px solid #00E18A;color:#fefdfe;font-weight:bold;font-style:italic\">Let's go</a></span></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table>";
            return content;
        }

    }
}
