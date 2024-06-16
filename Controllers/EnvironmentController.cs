using AutoMapper;
using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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


namespace divitiae_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class EnvironmentController : ControllerBase
    {
        private readonly IWorkEnvironmentServices _workEnvironmentServices;
        private readonly IUserServices _userServices;
        private readonly IUserToWorkEnvRoleServices _userToWorkEnvRoleServices;
        private readonly IWorkspaceServices _workspaceServices;
        public static WorkEnvironment environment = new WorkEnvironment();
        private readonly SQLDataContext _context;
        private readonly IConfiguration _configuration;
        private readonly IMapper _iMapper;
        private readonly IHttpContextAccessor _httpContentAccessor;

        public EnvironmentController(SQLDataContext context, IWorkspaceServices workspaceServices, IUserToWorkEnvRoleServices uToWERoleServices, IUserServices userServices, IHttpContextAccessor httpContentAccessor, IWorkEnvironmentServices workEnvironmentServices, IConfiguration configuration, IMapper iMapper)
        {
            _workEnvironmentServices = workEnvironmentServices;
            _configuration = configuration;
            _iMapper = iMapper;
            _httpContentAccessor = httpContentAccessor;
            _context = context;
            _userServices = userServices;
            _userToWorkEnvRoleServices = uToWERoleServices;
            _workspaceServices = workspaceServices;
        }

        /// <summary>
        /// Devuelve una lista de todos los environments.
        /// </summary>
        /// <returns>Lista de WorkEnvironments</returns>
        [HttpGet("all")]
        public async Task<IActionResult> GetAllEnvironments()
        {
            return Ok(await _workEnvironmentServices.GetAllEnvironments());
        }

        /// <summary>
        /// Devuelve la información del environment con ID id mapeada en un 
        /// workEnvironmentDTO.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>WorkEnvironmentDTO</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetEnvironmentById(string id)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);
                WorkEnvironmentDTO weData = await _userToWorkEnvRoleServices.CanAccessWorkEnvironment(user.Id.ToString(), id);
                return Ok(new { weData } );
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
        /// Recibe el nombre del nuevo environment y lo inserta en base de datos.
        /// </summary>
        /// <param name="environmentName"></param>
        /// <returns>ID del nuevo WorkEnvironment</returns>
        [HttpPost("add")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        public async Task<IActionResult> InsertEnvironment([FromBody] string environmentName)
        {

            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User user = await _userServices.GetUserByEmail(result);

            WorkEnvironment environment = new WorkEnvironment() { EnvironmentName = environmentName };

            UserToWorkEnvRole usrToWRRole = new UserToWorkEnvRole
            {
                User = user,
                WorkEnvironment = environment,
                IsAdmin = true,
                IsOwner = true,
            };

            user.UserToWorkEnvRole.Add(usrToWRRole);
            environment.UserToWorkEnvRole.Add(usrToWRRole);
            await _workEnvironmentServices.InsertEnvironment(environment);
            await _context.SaveChangesAsync();
            return Ok(environment.Id);
        }

        /// <summary>
        /// Recibe el nombre nuevo del environment y su ID. Revisa
        /// si el usuario actual tiene permisos para modificar el environment. Si
        /// es así, actualiza el nombre en base de datos.
        /// </summary>
        /// <param name="newName"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPut("change-name/{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateEnvironmentName([FromBody] string newName, string id)
        {
            if (newName == null) { return BadRequest(); }

            try
            {
                await UserCanModifyEnvrionment(id);
                WorkEnvironment we = await _workEnvironmentServices.GetEnvironmentById(id);
                we.EnvironmentName = newName;
                await _workEnvironmentServices.UpdateEnvironment(we);

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
        /// Recibe el ID de un environment. Revisa si el usuario actual puede
        /// modificar el environment. Si es así, lo elimina.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>NoContent(204)</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteEnvironment(string id)
        {
            try
            {
                await UserCanModifyEnvrionment(id);
                await _workEnvironmentServices.DeleteEnvironment(id);
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
        /// Revisa si el usuario actual puede modificar el environment con ID environmentId
        /// </summary>
        /// <param name="envrionmentId"></param>
        /// <returns></returns>
        private async Task UserCanModifyEnvrionment(string envrionmentId)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User user = await _userServices.GetUserByEmail(result);

            await _userToWorkEnvRoleServices.CanModifyWorkEnvironment(user.Id.ToString(), envrionmentId);
        }

    }
}
