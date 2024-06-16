using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Models.Mailing;
using divitiae_api.Services;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace divitiae_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AppTaskController : ControllerBase
    {
        private readonly IAppServices _appServices;
        private readonly IItemServices _itemServices;
        private readonly IWorkspaceServices _workspaceServices;
        private readonly IUserServices _userServices;
        private readonly IWorkEnvironmentServices _workEnvironmentServices;
        private readonly IAppTaskServices _appTaskServices;
        public static App app = new App();
        private readonly MongoClient _divitiaeClient ;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContentAccessor;
        private readonly IEmailServices _emailServices;


        public AppTaskController(IHttpContextAccessor httpContentAccessor, SQLDataContext context, IOptions<MongoDBSettings> mongoDBSettings, IAppTaskServices appTaskServices, IAppServices appServices, IConfiguration configuration, IItemServices itemServices, IWorkspaceServices workspaceServices, IUserServices userServices, IWorkEnvironmentServices workEnvironmentServices, IEmailServices emailServices)
        {
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            _appServices = appServices;
            _configuration = configuration;
            _itemServices = itemServices;
            _workspaceServices = workspaceServices;
            _httpContentAccessor = httpContentAccessor;
            _userServices = userServices;
            _appTaskServices = appTaskServices;
            _workEnvironmentServices = workEnvironmentServices;
            _emailServices = emailServices;
        }

        /// <summary>
        /// Recibe por parámetros el ID userId de un usuario y el ID environmentId de un environment.
        /// Busca en base de datos las tareas que contengan ambos campos y las devuelve en una lista.
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="environmentId"></param>
        /// <returns>Lista de AppTask</returns>
        [HttpGet("user")]
        public async Task<IActionResult> GetUserTasksByEnvironment([FromQuery] string userId, [FromQuery] string environmentId)
        {
            List<AppTask> tasks;
            if (!string.IsNullOrEmpty(userId) && !string.IsNullOrEmpty(environmentId))
            {
                tasks = await _appTaskServices.GetAppTaskByUserAndEnvironment(userId, environmentId);
            }
            else if (!string.IsNullOrEmpty(userId))
            {
                tasks = await _appTaskServices.GetAppTasksByUser(userId);
            }
            else
            {
                return BadRequest("Please provide at least one query parameter.");
            }

            return Ok(tasks);
        }

        /// <summary>
        /// Recibe un appTaskDTOCreate. Lo mapea a un appTask y lo inserta en base de datos.
        /// Envía mail al usuario asignado indicandole que tiene una nueva tarea asignada.
        /// Se crea una appTaskDTO basada en la nueva appTask y la devuelve.
        /// </summary>
        /// <param name="taskDTO"></param>
        /// <returns>AppTaskDTO</returns>
        [HttpPost("add")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> CreateTask([FromBody] AppTaskDTOCreate taskDTO)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                AppTask appTask = await _appTaskServices.mapAppTaskDTOCreate(user, taskDTO);

                await _appTaskServices.InsertAppTask(appTask);

                MailRequest mailRequest = new MailRequest();
                mailRequest.ToEmail = appTask.AssignedUser.Email;
                mailRequest.Subject = "You just got assigned a new task!";
                mailRequest.Body = BuildNewTaskNotificationHtmlContent(appTask.AssignedUser.Name, user.Name, user.Id.ToString());
                await _emailServices.SendEmailAsync(mailRequest);

                AppTaskDTO appTaskDTO = new AppTaskDTO()
                {
                    Id = appTask.Id.ToString(),
                    CreatedBy = appTask.CreatedBy,
                    CreatedOn = appTask.CreatedOn,
                    Environment = appTask.Environment,
                    AssignedUser = appTask.AssignedUser,
                    Workspace = appTask.Workspace,
                    App = appTask.App,
                    Item = appTask.Item,
                    DueDate = taskDTO.DueDate,
                    Information = taskDTO.Information,
                    Finished = taskDTO.Finished
                };

                return Created("Created", appTaskDTO);
                
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (CustomBadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
        }


        /// <summary>
        /// Recibe un appTaskDTOCreate y el ID de la appTask que representa. Revisa si el
        /// usuario actual es el creador de la appTask. Si es así, la actualiza. Se crea una
        /// appTaskDTO basada en la nueva appTask y se devuelve.
        /// </summary>
        /// <param name="taskDTO"></param>
        /// <param name="id"></param>
        /// <returns>AppTask</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateAppTask([FromBody] AppTaskDTOCreate taskDTO, string id)
        {
            try
            {
                if (taskDTO == null) { return BadRequest(); }

                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                AppTask ogTask = await _appTaskServices.GetAppTaskById(id);

                if (user.Id.ToString() != ogTask.CreatedBy.Id) 
                {
                    throw new NoAccessException(user.Id.ToString(), "Task", id);
                }

                AppTask appTask = await _appTaskServices.mapAppTaskDTOCreate(user, taskDTO);
                appTask.Id = ogTask.Id;
                await _appTaskServices.UpdateAppTask(appTask);

                AppTaskDTO appTaskDTO = new AppTaskDTO()
                {
                    Id = appTask.Id.ToString(),
                    CreatedBy = appTask.CreatedBy,
                    CreatedOn = appTask.CreatedOn,
                    Environment = appTask.Environment,
                    AssignedUser = appTask.AssignedUser,
                    Workspace = appTask.Workspace,
                    App = appTask.App,
                    Item = appTask.Item,
                    DueDate = taskDTO.DueDate,
                    Information = taskDTO.Information,
                    Finished = taskDTO.Finished
                };


                return Created("Created", appTaskDTO);

            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (CustomBadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Recibe el id de una appTask y su nuevo valor para el campo finished. Revisa
        /// si el usuario actual es o el creador o el usuario asignado de la appTask. Si es así,
        /// actualiza la appTask en base de datos.
        /// </summary>
        /// <param name="id"></param>
        /// <param name="status"></param>
        /// <returns>Created(201)</returns>
        [HttpPatch("{id}/status")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult>CompletedeAppTask(string id, [FromBody] bool status)
        {
            try
            {
                if (id == null || id == "") { return BadRequest(); }

                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                AppTask ogTask = await _appTaskServices.GetAppTaskById(id);

                if (user.Id.ToString() != ogTask.CreatedBy.Id && user.Id.ToString() != ogTask.AssignedUser.Id)
                {
                    throw new NoAccessException(user.Id.ToString(), "Task", ogTask.Id.ToString());
                }

                ogTask.Finished = status;
                await _appTaskServices.UpdateAppTask(ogTask);

                User otherUser = user.Id.ToString() != ogTask.CreatedBy.Id
                    ? await _userServices.GetUserById(ogTask.CreatedBy.Id)
                    : await _userServices.GetUserById(ogTask.AssignedUser.Id);

                if (status)
                {
                    MailRequest mailRequest = new MailRequest();
                    mailRequest.ToEmail = otherUser.Email;
                    mailRequest.Subject = "One of your tasks got completed!";
                    mailRequest.Body = BuildCompletedTaskNotificationHtmlContent(otherUser.Name, user.Name, ogTask.Information);
                    await _emailServices.SendEmailAsync(mailRequest);
                }

                return Created("Created", true);

            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (CustomBadRequestException ex)
            {
                return BadRequest(ex.Message);
            }
        }

        /// <summary>
        /// Recibe un taskCommentDTO y el ID de la appTask. Revisa si el usuario 
        /// actual es o el creador o el usuario asignado de la appTask. Si es así,
        /// añade el comentario a la appTask en base de datos y manda un email al
        /// otro usuario de la appTask para avisarle del nuevo comentario.
        /// </summary>
        /// <param name="comment"></param>
        /// <param name="id"></param>
        /// <returns></returns>
        [HttpPut("{id}/add-comment")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CommentAppTask([FromBody] TaskCommentDTO comment, string id)
        {
            try
            {
                if (comment == null) { return BadRequest(); }

                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                AppTask task = await _appTaskServices.GetAppTaskById(id);

                if (user.Id.ToString() != task.CreatedBy.Id && user.Id.ToString() != task.AssignedUser.Id)
                {
                    throw new NoAccessException(user.Id.ToString(), "Task", task.Id.ToString());
                }


                User otherUser = user.Id.ToString() != task.CreatedBy.Id
                    ? await _userServices.GetUserById(task.CreatedBy.Id)
                    : await _userServices.GetUserById(task.AssignedUser.Id);

                task.Comments.Add(comment);

                await _appTaskServices.UpdateAppTask(task);

                MailRequest mailRequest = new MailRequest();
                mailRequest.ToEmail = otherUser.Email;
                mailRequest.Subject = "You just got assigned a new task!";
                mailRequest.Body = BuildNewCommentNotificationHtmlContent(otherUser.Name, user.Name, user.Id.ToString(), comment);
                await _emailServices.SendEmailAsync(mailRequest);

                return Created("Created", true);

            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
        }

        private string BuildNewTaskNotificationHtmlContent(string name, string taskCretorName,string id)
        {
            string content = "<table style=\"padding: 30px; background-color: #00E18A; border-radius: 20px;\" class=\"es-wrapper\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tbody><tr><td class=\"esd-email-paddings\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"es-content\" align=\"center\"><tbody><tr><td class=\"esd-stripe\" align=\"center\"><table style=\"padding: 20px; border-radius: 20px; background-color: white;font-family:'Gordita',Helvetica,Arial,sans-serif!important\" class=\"es-content-body\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\" width=\"600\"><tbody><tr><td class=\"esd-structure es-p15t es-p30b es-p20r es-p20l\" align=\"left\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td width=\"560\" class=\"esd-container-frame\" align=\"center\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td align=\"center\" class=\"esd-block-image es-p10t es-p10b\" style=\"font-size: 0px;\"><a target=\"_blank\"><img src=\"https://fiigjir.stripocdn.email/content/guids/CABINET_fc3ee6f268148614767e499f231f5b5bb667bc3ba605dd1e448c87775b6cafc0/images/divitiaecolor.png\" alt=\"\" style=\"display:block\" width=\"325\" class=\"adapt-img\"></a></td></tr><tr><td align=\"center\" class=\"esd-block-text es-p10t es-p10b es-p40r es-p40l es-m-p0r es-m-p0l\"><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Hello, " + name + "!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">"+taskCretorName+" just assigned you a task!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Review your pending tasks in Divitiae!</p></td></tr><tr><td align=\"center\" class=\"esd-block-button es-p5t es-p5b\"><span class=\"es-button-border\" style=\"font-family:'Gordita',Helvetica,Arial,sans-serif!important\"><a href=\"http://localhost:8080/login\" class=\"es-button es-button-4663\" target=\"_blank\" style=\"text-decoration: none; padding: 10px 30px 10px 30px; border-radius: 20px; font-size: 20px; font-weight: bold; background:#00E18A;mso-border-alt:10px solid #00E18A;color:#fefdfe;font-weight:bold;font-style:italic\">Let's go</a></span></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table>";
            return content;
        }

        private string BuildNewCommentNotificationHtmlContent(string name, string taskCretorName, string id, TaskCommentDTO comment)
        {
            string content = "<table style=\"padding: 30px; background-color: #00E18A; border-radius: 20px;\" class=\"es-wrapper\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tbody><tr><td class=\"esd-email-paddings\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"es-content\" align=\"center\"><tbody><tr><td class=\"esd-stripe\" align=\"center\"><table style=\"padding: 20px; border-radius: 20px; background-color: white;font-family:'Gordita',Helvetica,Arial,sans-serif!important\" class=\"es-content-body\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\" width=\"600\"><tbody><tr><td class=\"esd-structure es-p15t es-p30b es-p20r es-p20l\" align=\"left\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td width=\"560\" class=\"esd-container-frame\" align=\"center\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td align=\"center\" class=\"esd-block-image es-p10t es-p10b\" style=\"font-size: 0px;\"><a target=\"_blank\"><img src=\"https://fiigjir.stripocdn.email/content/guids/CABINET_fc3ee6f268148614767e499f231f5b5bb667bc3ba605dd1e448c87775b6cafc0/images/divitiaecolor.png\" alt=\"\" style=\"display:block\" width=\"325\" class=\"adapt-img\"></a></td></tr><tr><td align=\"center\" class=\"esd-block-text es-p10t es-p10b es-p40r es-p40l es-m-p0r es-m-p0l\"><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Hello, " + name + "!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">" + taskCretorName + " made a comment on one of your tasks:</p><div style=\"padding:5px;border:1px solid black;border-radius:5px\"><p style=\"font-weight:bold\">"+ taskCretorName + ":</p><p>"+comment.Comment+"</p></div><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Review your pending tasks in Divitiae!</p></td></tr><tr><td align=\"center\" class=\"esd-block-button es-p5t es-p5b\"><span class=\"es-button-border\" style=\"font-family:'Gordita',Helvetica,Arial,sans-serif!important\"><a href=\"http://localhost:8080/login\" class=\"es-button es-button-4663\" target=\"_blank\" style=\"text-decoration: none; padding: 10px 30px 10px 30px; border-radius: 20px; font-size: 20px; font-weight: bold; background:#00E18A;mso-border-alt:10px solid #00E18A;color:#fefdfe;font-weight:bold;font-style:italic\">Let's go</a></span></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table>";
            return content;
        }

        private string BuildCompletedTaskNotificationHtmlContent(string name, string taskCretorName, string information)
        {
            string content = "<table style=\"padding: 30px; background-color: #00E18A; border-radius: 20px;\" class=\"es-wrapper\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\"><tbody><tr><td class=\"esd-email-paddings\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" class=\"es-content\" align=\"center\"><tbody><tr><td class=\"esd-stripe\" align=\"center\"><table style=\"padding: 20px; border-radius: 20px; background-color: white;font-family:'Gordita',Helvetica,Arial,sans-serif!important\" class=\"es-content-body\" align=\"center\" cellpadding=\"0\" cellspacing=\"0\" width=\"600\"><tbody><tr><td class=\"esd-structure es-p15t es-p30b es-p20r es-p20l\" align=\"left\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td width=\"560\" class=\"esd-container-frame\" align=\"center\" valign=\"top\"><table cellpadding=\"0\" cellspacing=\"0\" width=\"100%\"><tbody><tr><td align=\"center\" class=\"esd-block-image es-p10t es-p10b\" style=\"font-size: 0px;\"><a target=\"_blank\"><img src=\"https://fiigjir.stripocdn.email/content/guids/CABINET_fc3ee6f268148614767e499f231f5b5bb667bc3ba605dd1e448c87775b6cafc0/images/divitiaecolor.png\" alt=\"\" style=\"display:block\" width=\"325\" class=\"adapt-img\"></a></td></tr><tr><td align=\"center\" class=\"esd-block-text es-p10t es-p10b es-p40r es-p40l es-m-p0r es-m-p0l\"><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Hello, " + name + "!</p><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">" + taskCretorName + " marked this task as completed:</p><div style=\"padding:5px;border:1px solid black;border-radius:5px\"><p style=\"font-weight:bold\"></p><p>"+information+"</p></div><p style=\"font-size: 16px;font-family:'Gordita',Helvetica,Arial,sans-serif!important\">Review your pending tasks in Divitiae!</p></td></tr><tr><td align=\"center\" class=\"esd-block-button es-p5t es-p5b\"><span class=\"es-button-border\" style=\"font-family:'Gordita',Helvetica,Arial,sans-serif!important\"><a href=\"http://localhost:8080/login\" class=\"es-button es-button-4663\" target=\"_blank\" style=\"text-decoration: none; padding: 10px 30px 10px 30px; border-radius: 20px; font-size: 20px; font-weight: bold; background:#00E18A;mso-border-alt:10px solid #00E18A;color:#fefdfe;font-weight:bold;font-style:italic\">Let's go</a></span></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table></td></tr></tbody></table>";
            return content;
        }

    }
}
