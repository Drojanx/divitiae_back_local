using AutoMapper;
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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using Newtonsoft.Json;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;


namespace divitiae_api.Controllers
{
    [Route("api/environment")]
    [ApiController]
    [Authorize]
    public class UserToWorkEnvController : ControllerBase
    {
        private readonly IWorkEnvironmentServices _workEnvironmentServices;
        private readonly IUserToWorkEnvRoleServices _userToWorkEnvRoleServices;
        private readonly IUserServices _userServices;
        private readonly IUserToWorkEnvRoleServices _uToWERoleServices;
        private readonly IAppTaskServices _appTaskServices;
        private readonly IWorkspaceServices _workspaceServices;
        private readonly IEmailServices _emailServices;
        public static WorkEnvironment environment = new WorkEnvironment();
        private readonly SQLDataContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMapper _iMapper;
        private readonly IHttpContextAccessor _httpContentAccessor;

        public UserToWorkEnvController(IEmailServices emailServices, SQLDataContext context, IWorkspaceServices workspaceServices, IUserToWorkEnvRoleServices uToWERoleServices, IUserServices userServices, IHttpContextAccessor httpContentAccessor, IWorkEnvironmentServices workEnvironmentServices, IConfiguration configuration, IMapper iMapper, IUserToWorkEnvRoleServices userToWorkEnvRoleServices, IAppTaskServices appTaskServices)
        {
            _workEnvironmentServices = workEnvironmentServices;
            _configuration = configuration;
            _iMapper = iMapper;
            _httpContentAccessor = httpContentAccessor;
            _context = context;
            _userServices = userServices;
            _uToWERoleServices = uToWERoleServices;
            _workspaceServices = workspaceServices;
            _emailServices = emailServices;
            _userToWorkEnvRoleServices = userToWorkEnvRoleServices;
            _appTaskServices = appTaskServices;
        }

        /// <summary>
        /// Revisa que el usuario actual tiene permisos de admin en el environment con ID weId. Si es así,
        /// crea en base de datos una relación entre el user indicado en el accessToEnvironmentDTO y el environment. También
        /// entre el user y los workspaces indicados en el accessToEnvironmentDTO. Devuelve un workEnvironmentUserDataDTO
        /// según la información de acceso proporcionada. Envía también un email al nuevo usuario indicándole que ha sido 
        /// invitado a un nuevo environment.
        /// </summary>
        /// <param name="aToENDTO"></param>
        /// <param name="weId"></param>
        /// <returns>WorkEnvironmentUserDataDTO</returns>
        [HttpPost("{weId}/add-user")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> AddUserToEnv([FromBody] AccessToEnvironmentDTO aToENDTO, string weId)
        {
            if (aToENDTO == null) { return BadRequest(); }

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                await UserCanModifyEnvrionment(weId);
                WorkEnvironment we = await _workEnvironmentServices.GetEnvironmentById(weId);
                if (await _uToWERoleServices.UserToWorkEnvRoleExists(aToENDTO.UserId, weId))
                    return BadRequest($"User {aToENDTO.UserId} already has access to Work Environment {weId}");

                UserToWorkEnvRole uToWERole = new UserToWorkEnvRole
                {
                    UserId = new Guid(aToENDTO.UserId),
                    WorkEnvironmentId = new Guid(weId),
                    IsAdmin = aToENDTO.IsAdmin,
                    IsOwner = aToENDTO.IsOwner
                };

                User u = await _userServices.GetUserById(aToENDTO.UserId);

                foreach (var wsId in aToENDTO.WorkspaceIds)
                {                    
                    Workspace ws = await _workspaceServices.GetWorkspaceById(wsId);
                    if (ws.WorkenvironmentId.ToString() != weId)
                        return BadRequest($"Workspace {wsId} doesn't belong to Work Environment {weId}");
                    u.Workspaces.Add(ws);
                }

                u.UserToWorkEnvRole.Add(uToWERole);
                we.UserToWorkEnvRole.Add(uToWERole);

                await _context.SaveChangesAsync();
                transaction.Commit();
                WorkEnvironmentUserDataDTO userData = new WorkEnvironmentUserDataDTO()
                {
                    UserId = u.Id,
                    UserEmail = u.Email,
                    UserDisplayName = u.Name + " " + u.LastName,
                    IsAdmin = uToWERole.IsAdmin,
                    IsOwner=uToWERole.IsOwner
                };

                MailRequest mailRequest = new MailRequest();
                mailRequest.ToEmail = u.Email;
                mailRequest.Subject = "Divitiae - Invitation to the " + we.EnvironmentName + " environment";
                mailRequest.Body = BuildWelcomeHtmlContent(u.Name, we.EnvironmentName);
                await _emailServices.SendEmailAsync(mailRequest);

                User u2 = await _userServices.GetUserById(aToENDTO.UserId);
                return Created("Created", userData);
            }
            catch (ItemNotFoundException ex)
            {
                transaction.Rollback();
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
            catch (NoAdminPermissionException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
        }

        /// <summary>
        /// Revisa que el user actual tiene permisos de edición en el environment con ID weId. Si es así,
        /// actualiza el permiso de admin del usuario con ID userID del environment.Devuelve un workEnvironmentUserDataDTO
        /// según la información de acceso del user. Envía también un email al nuevo usuario indicándole que su rol de admin
        /// ha sido actualizado.
        /// </summary>
        /// <param name="adminPermission"></param>
        /// <param name="weId"></param>
        /// <param name="userId"></param>
        /// <returns>WorkEnvironmentUserDataDTO</returns>
        [HttpPut("{weId}/admin-permission/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateUserAdminPermission([FromBody] bool adminPermission, string weId, string userId)
        {
            if (adminPermission == null) { return BadRequest(); }

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                User user = await UserCanModifyEnvrionment(weId);

                WorkEnvironment we = await _workEnvironmentServices.GetEnvironmentById(weId);
                if (!await _uToWERoleServices.UserToWorkEnvRoleExists(userId, weId))
                    return BadRequest($"The user you are trying to update is not a member of the environment");

                UserToWorkEnvRole uToWERole = await _uToWERoleServices.GetUserToWorkEnvRoleByUserIdAndWorkEnvId(weId, userId);
                uToWERole.IsAdmin = adminPermission;        

                await _context.SaveChangesAsync();
                transaction.Commit();
                WorkEnvironmentUserDataDTO userData = new WorkEnvironmentUserDataDTO()
                {
                    UserId = uToWERole.UserId,
                    UserEmail = uToWERole.User.Email,
                    UserDisplayName = uToWERole.User.Name + " " + uToWERole.User.LastName,
                    IsAdmin = uToWERole.IsAdmin,
                    IsOwner = uToWERole.IsOwner
                };
                MailRequest mailRequest = new MailRequest();
                mailRequest.ToEmail = userData.UserEmail;
                mailRequest.Subject = $"{user.Name} updated your administrator role!";
                string isAdminOrNot = adminPermission ? $"making you an administrator." : $"revoking your administrator role.";
                string message = $"{user.Name} just updated your permissions on the <span style='font-weight: bold'>{we.EnvironmentName}</span> environment "+ isAdminOrNot;
                mailRequest.Body = BuildOwnerOrAdminRoleNotificationHtmlContent(userData.UserDisplayName, message);
                await _emailServices.SendEmailAsync(mailRequest);

                return Created("Created", userData);
            }
            catch (ItemNotFoundException ex)
            {
                transaction.Rollback();
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
            catch (NoAdminPermissionException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
        }

        /// <summary>
        /// Revisa que el user actual tiene permisos de edición en el environment con ID weId. Si es así y además en owner,
        /// actualiza el permiso de owner del user con ID userID del environment. Devuelve un workEnvironmentUserDataDTO
        /// según la información de acceso del user. Envía también un email al user indicándole que su rol de owner
        /// ha sido actualizado.
        /// </summary>
        /// <param name="adminPermission"></param>
        /// <param name="weId"></param>
        /// <param name="userId"></param>
        /// <returns>WorkEnvironmentUserDataDTO</returns>
        [HttpPut("{weId}/owner-permission/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateUserOwnerPermission([FromBody] bool ownerPermission, string weId, string userId)
        {
            if (ownerPermission == null) { return BadRequest(); }

            using var transaction = _context.Database.BeginTransaction();

            try
            {
                User currentUser = await UserCanModifyEnvrionment(weId);
                WorkEnvironment we = await _workEnvironmentServices.GetEnvironmentById(weId);
                
                if (!await _uToWERoleServices.UserToWorkEnvRoleExists(userId, weId))
                    return BadRequest($"The user you are trying to update is not a member of the environment.");
                
                UserToWorkEnvRole currentUserRole = await _uToWERoleServices.GetUserToWorkEnvRoleByUserIdAndWorkEnvId(weId, currentUser.Id.ToString());

                if (!currentUserRole.IsOwner)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, "Only owners can modifify other users' owner status.");
                }

                UserToWorkEnvRole editingUserRole = await _uToWERoleServices.GetUserToWorkEnvRoleByUserIdAndWorkEnvId(weId, userId);
                editingUserRole.IsOwner = ownerPermission;

                await _context.SaveChangesAsync();
                transaction.Commit();
                WorkEnvironmentUserDataDTO userData = new WorkEnvironmentUserDataDTO()
                {
                    UserId = editingUserRole.UserId,
                    UserEmail = editingUserRole.User.Email,
                    UserDisplayName = editingUserRole.User.Name + " " + editingUserRole.User.LastName,
                    IsAdmin = editingUserRole.IsAdmin,
                    IsOwner = editingUserRole.IsOwner
                };
                MailRequest mailRequest = new MailRequest();
                mailRequest.ToEmail = userData.UserEmail;
                mailRequest.Subject = $"{currentUser.Name} updated your owner status!";
                string isAdminOrNot = ownerPermission ? $"making you an owner." : $"revoking your owner status.";
                string message = $"{currentUser.Name} just updated your permissions on the <span style='font-weight: bold'>{we.EnvironmentName}</span> environment " + isAdminOrNot;
                mailRequest.Body = BuildOwnerOrAdminRoleNotificationHtmlContent(userData.UserDisplayName, message);
                await _emailServices.SendEmailAsync(mailRequest);

                return Created("Created", userData);
            }
            catch (ItemNotFoundException ex)
            {
                transaction.Rollback();
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
            catch (NoAdminPermissionException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
        }

        /// <summary>
        /// Revisa que el user actual tiene permisos de edición en el environment con ID weId. Si el usuario
        /// siendo eliminado NO es owner, lo elimina del environment.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="userId"></param>
        /// <returns>NoContent(204)</returns>
        [HttpDelete("{id}/remove-user/{userId}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> RemoveCurrentUserFromEnvironment(string id, string userId)
        {
            using var transaction = _context.Database.BeginTransaction();

            try
            {
                User user = await UserCanModifyEnvrionment(id);
                await CheckIfOwnerIsBeingRemoved(id, userId);
                await _uToWERoleServices.DeleteUserToWorkEnvRole(userId, id);

                List<AppTask> tasks = await _appTaskServices.GetAppTaskByUserAndEnvironment(userId, id);

                foreach (AppTask task in tasks)
                {
                    await _appTaskServices.DeleteAppTask(task.Id.ToString());
                }

                transaction.Commit();
                return NoContent();
            }
            catch (ItemNotFoundException ex)
            {
                transaction.Rollback();
                return NotFound(ex.Message);
            }
            catch (BadHttpRequestException ex)
            {
                transaction.Rollback();
                return BadRequest(ex.Message);
            }
            catch (NoAdminPermissionException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
            catch (NoAccessException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
            catch (DeletingOtherOwnerException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
            catch (DeletingLastOwnerException ex)
            {
                transaction.Rollback();
                return StatusCode(403, ex.Message);
            }
        }

        /// <summary>
        /// Revisa si el usuario siendo eliminado es owner. Si el usuario actual se está
        /// intentando eliminar a sí mismo, sólo se eliminará si hay más owners en el environment,
        /// ya que no puede haber un environment sin owner. 
        /// Si el usuario a eliminares owner y no es el mismo usuario haciendo la petición, se lanzará una excepción
        /// ya que los owners sólo pueden eliminarse ellos mismos.
        /// </summary>
        /// <param name="weId"></param>
        /// <param name="userId"></param>
        /// <exception cref="DeletingOtherOwnerException"></exception>
        /// <exception cref="DeletingLastOwnerException"></exception>
        private async Task CheckIfOwnerIsBeingRemoved(string weId, string userId)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User user = await _userServices.GetUserByEmail(result);

            User userToRemove = await _userServices.GetUserById(userId);
            bool isOwner = await _userToWorkEnvRoleServices.UserIsOwner(weId, userId);
            if (user.Id.ToString() != userId && isOwner) 
            {
                throw new DeletingOtherOwnerException();
            } else if (user.Id.ToString() == userId && isOwner){
                List<UserToWorkEnvRole> owners = await _userToWorkEnvRoleServices.GetEveryOwnerRole(weId);
                if (owners.Count == 1)
                {
                    throw new DeletingLastOwnerException();
                }
            }
        }

        /// <summary>
        /// Revisa si el usuario actual tiene permisos de edición en el environment con ID environmentId
        /// </summary>
        /// <param name="envrionmentId"></param>
        /// <returns></returns>
        private async Task<User> UserCanModifyEnvrionment(string environmentId)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User user = await _userServices.GetUserByEmail(result);

            await _userToWorkEnvRoleServices.CanModifyWorkEnvironment(user.Id.ToString(), environmentId);

            return user;
        }

        private string BuildWelcomeHtmlContent(string name, string envName)
        {
            string content = "<table style=\"padding: 30px; background-color: #00E18A; border-radius: 20px;\" class=\"es-wrapper\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tbody><tr><td class=\"esd-email-paddings\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"es-content\" align=\"center\"><tbody><tr><td class=\"esd-stripe\" align=\"center\"><table style=\"padding: 20px; border-radius: 20px; background-color: white;font-family:'Gordita',Helvetica,Arial,sans-serif!important\" class=\"es-content-body\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\" width=\"600\"><tbody><tr><td class=\"esd-structure es-p15t es-p30b es-p20r es-p20l\" align=\"left\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td width=\"560\" class=\"esd-container-frame\" align=\"center\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td align=\"center\" class=\"esd-block-image es-p10t es-p10b\" style=\"font-size: 0px;\"><a target=\"_blank\"><img src=\"https://fiigjir.stripocdn.email/content/guids/CABINET_fc3ee6f268148614767e499f231f5b5bb667bc3ba605dd1e448c87775b6cafc0/images/divitiaecolor.png\" alt=\"\" style=\"display:block\" width=\"325\" class=\"adapt-img\"></a></td></tr><tr><td align=\"center\" class=\"esd-block-text es-p10t es-p10b es-p40r es-p40l es-m-p0r es-m-p0l\"><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Hello, " + name + "!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">You've been invited to the " + envName + " environment!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Start collaborating with your team!</p></td></tr><tr><td align=\"center\" class=\"esd-block-button es-p5t es-p5b\"><span class=\"es-button-border\" style=\"font-family:'Gordita',Helvetica,Arial,sans-serif!important\"><a href=\"http://localhost:8080/login\" class=\"es-button es-button-4663\" target=\"_blank\" style=\"text-decoration: none; padding: 10px 30px 10px 30px; border-radius: 20px; font-size: 20px; font-weight: bold; background:#00E18A;mso-border-alt:10px solid #00E18A;color:#fefdfe;font-weight:bold;font-style:italic\">Let's go</a></span></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table>";
            return content;
        }

        private string BuildOwnerOrAdminRoleNotificationHtmlContent(string name, string message)
        {
            string content = "<table style=\"padding: 30px; background-color: #00E18A; border-radius: 20px;\" class=\"es-wrapper\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tbody><tr><td class=\"esd-email-paddings\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"es-content\" align=\"center\"><tbody><tr><td class=\"esd-stripe\" align=\"center\"><table style=\"padding: 20px; border-radius: 20px; background-color: white;font-family:'Gordita',Helvetica,Arial,sans-serif!important\" class=\"es-content-body\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\" width=\"600\"><tbody><tr><td class=\"esd-structure es-p15t es-p30b es-p20r es-p20l\" align=\"left\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td width=\"560\" class=\"esd-container-frame\" align=\"center\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td align=\"center\" class=\"esd-block-image es-p10t es-p10b\" style=\"font-size: 0px;\"><a target=\"_blank\"><img src=\"https://fiigjir.stripocdn.email/content/guids/CABINET_fc3ee6f268148614767e499f231f5b5bb667bc3ba605dd1e448c87775b6cafc0/images/divitiaecolor.png\" alt=\"\" style=\"display:block\" width=\"325\" class=\"adapt-img\"></a></td></tr><tr><td align=\"center\" class=\"esd-block-text es-p10t es-p10b es-p40r es-p40l es-m-p0r es-m-p0l\"><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Hello, " + name + "!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">"+ message+ "</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Review what's changed in Divitiae!</p></td></tr><tr><td align=\"center\" class=\"esd-block-button es-p5t es-p5b\"><span class=\"es-button-border\" style=\"font-family:'Gordita',Helvetica,Arial,sans-serif!important\"><a href=\"http://localhost:8080/login\" class=\"es-button es-button-4663\" target=\"_blank\" style=\"text-decoration: none; padding: 10px 30px 10px 30px; border-radius: 20px; font-size: 20px; font-weight: bold; background:#00E18A;mso-border-alt:10px solid #00E18A;color:#fefdfe;font-weight:bold;font-style:italic\">Let's go</a></span></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table>";
            return content;
        }

    }
}
