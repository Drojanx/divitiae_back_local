using AutoMapper;
using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services;
using divitiae_api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson.IO;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace divitiae_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WorkspaceController : ControllerBase
    {
        private readonly IUserServices _userServices;
        private readonly IWorkspaceServices _workspaceServices;
        private readonly IWorkEnvironmentServices _workEnvironmentServices;
        public static Workspace workspace = new Workspace();
        private readonly IAppTaskServices _appTaskServices;
        private readonly IConfiguration _configuration;
        private readonly IMapper _iMapper;
        private readonly IHttpContextAccessor _httpContentAccessor;

        public WorkspaceController(IHttpContextAccessor httpContentAccessor, IWorkspaceServices workspaceServices, IConfiguration configuration, IMapper iMapper, IUserServices userServices, IWorkEnvironmentServices workEnvironmentServices, IAppTaskServices appTaskServices)
        {
            _workspaceServices = workspaceServices;
            _configuration = configuration;
            _iMapper = iMapper;
            _httpContentAccessor = httpContentAccessor;
            _userServices = userServices;
            _workEnvironmentServices = workEnvironmentServices;
            _appTaskServices = appTaskServices;
        }

        /// <summary>
        /// Devuelve una lista de todos los workspaces
        /// </summary>
        /// <returns>Lista de Workspace</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllWorkspaces()
        {
            return Ok(await _workspaceServices.GetAllWorkspaces());
        }

        /// <summary>
        /// Devuelve el workspaceDTO del workspace con ID id en base de datos.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>WorkspaceDTO</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetWorkspaceById(string id)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);
                return Ok(await _workspaceServices.CanAccessWS(user.Id.ToString(), id));
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


        /// <summary>
        /// Revisa que el usuario actual tiene permisos para crear workspaces en en environment con ID environmentID.
        /// Si es así, lo inserta en base de datos y devuelve su workspaceDTO.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="environmentId"></param>
        /// <returns>WorkspaceDTO</returns>
        [HttpPost("{environmentId}/add")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateWorkspace([FromBody] WorkspaceDTOCreate request, string environmentId)
        {
            if (request == null)
                return BadRequest("Request is null");            

            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);
                await _workspaceServices.CanCreateWS(user.Id.ToString(), environmentId);
                WorkEnvironment we = await _workEnvironmentServices.GetEnvironmentById(environmentId);
                Workspace workspace = new Workspace() { WorkspaceName = request.WorkspaceName, WorkenvironmentId = we.Id, Users = new List<User> { user } };
                await _workspaceServices.InsertWorkspace(workspace);
                WorkspaceDTO workspaceDTO = new WorkspaceDTO
                {
                    WorkspaceName = request.WorkspaceName,
                    Id = workspace.Id.ToString(),
                    Apps = new List<AppDTO> { }
                };
                return Created("Created", workspaceDTO);
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

        /// <summary>
        /// Revisa si el usuario actual puede modifica el workspace con ID id en base de datos. Si es así,
        /// lo actualiza en base de datos.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateWorkspace([FromBody] Workspace request, string id)
        {
            if (request == null) { return BadRequest(); }

            try
            {
                await UserCanModifyWorkspace(id);

                request.Id = new Guid(id);
                await _workspaceServices.UpdateWorkspace(request);

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
            catch (NoAdminPermissionException ex)
            {
                return StatusCode(403, ex.Message);
            }

        }

        /// <summary>
        /// Revisa si el usuario actual puede modifica el workspace con ID id en base de datos. Si es así,
        /// actualiza su nombre con el valor de newWorkspaceName en base de datos.
        /// </summary>
        /// <param name="newWorkspaceName"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPatch("{id}/edit-name")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateWorkspaceName([FromBody] string newWorkspaceName, string id)
        {
            if (newWorkspaceName == null) { return BadRequest(); }

            try
            {
                await UserCanModifyWorkspace(id);
                Workspace ws = await _workspaceServices.GetWorkspaceById(id);
                ws.WorkspaceName = newWorkspaceName;
                await _workspaceServices.UpdateWorkspace(ws);

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
            catch (NoAdminPermissionException ex)
            {
                return StatusCode(403, ex.Message);
            }

        }

        /// <summary>
        /// Revisa si el usuario actual puede modificar el workspace con ID id en base de datos. Si
        /// es así, lo elimina junto con sus tareas y apps asociadas.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>NoContent(204)</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteWorkspace(string id)
        {
            try
            {
                await UserCanModifyWorkspace(id);

                await _workspaceServices.DeleteWorkspace(id);

                List<AppTask> tasks = await _appTaskServices.GetAppTasksByWorkspace(id);

                foreach (AppTask task in tasks)
                {
                    await _appTaskServices.DeleteAppTask(task.Id.ToString());
                }

                return NoContent();
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            catch (NoAccessException ex)
            {
                return StatusCode(403, ex.Message);
            }
            catch (NoAdminPermissionException ex)
            {
                return StatusCode(403, ex.Message);
            }
        }

        /// <summary>
        /// Revisa que el usuario actual tiene permisos de edición en el workspace con ID workspaceId en base de datos.
        /// </summary>
        /// <param name="workspaceId"></param>
        private async Task UserCanModifyWorkspace(string workspaceId)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User user = await _userServices.GetUserByEmail(result);

            await _workspaceServices.CanModifyWS(user.Id.ToString(), workspaceId);
        }

    }
}
