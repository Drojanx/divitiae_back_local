using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services;
using divitiae_api.Services.Interfaces;
using divitiae_api.SQLData;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MongoDB.Bson;
using MongoDB.Driver;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace divitiae_api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class AppController : ControllerBase
    {
        private readonly IAppServices _appServices;
        private readonly IItemServices _itemServices;
        private readonly IWorkspaceServices _workspaceServices;
        private readonly IAppTaskServices _appTaskServices;
        private readonly IUserServices _userServices;
        public static App app = new App();
        private readonly MongoClient _divitiaeClient ;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContentAccessor;


        public AppController(IHttpContextAccessor httpContentAccessor, SQLDataContext context, IOptions<MongoDBSettings> mongoDBSettings, IAppServices appServices, IConfiguration configuration, IItemServices itemServices, IWorkspaceServices workspaceServices, IUserServices userServices, IAppTaskServices appTaskServices)
        {
            _divitiaeClient = new MongoClient(mongoDBSettings.Value.ConnectionURI);
            _appServices = appServices;
            _configuration = configuration;
            _itemServices = itemServices;
            _workspaceServices = workspaceServices;
            _httpContentAccessor = httpContentAccessor;
            _userServices = userServices;
            _appTaskServices = appTaskServices;
        }

        /// <summary>
        /// Devuelve una lista con todas las apps.
        /// </summary>
        /// <returns>Lista de Apps</returns>
        [HttpGet]
        public async Task<IActionResult> GetAllApps()
        {
            return Ok(await _appServices.GetAllApps());
        }

        /// <summary>
        /// Devuelve la app con ID id
        /// </summary>
        /// <param name="id"></param>
        /// <returns>App</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAppById(string id)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), id);
                return Ok(await _appServices.GetAppById(id));
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
        /// Recibe un appDTOCreate para crear una app e insertarla en el workspace con ID workspaceId.
        /// Primero se revisa que no haya 2 fields con el mismo nombre, si hay alguno lanza un BadRequest.
        /// Luego, se revisa que el usuario actual tenga los permisos necesarios para crear una app en este
        /// workspace. Si puede, se mapea el DTO a una app nueva, se crea la entidad de relación WsAppsRelations
        /// y se actualiza todo en base de datos.
        /// </summary>
        /// <param name="request"></param>
        /// <param name="workspaceId"></param>
        /// <returns>Created(201)</returns>
        [HttpPost("add-to/{workspaceId}")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateApp([FromBody] AppDTOCreate request, string workspaceId)
        {
            if (request == null) 
                return BadRequest("Request is null"); 

            List<string> fieldNames = new List<string>();
            HashSet<string> uniqueElements = new HashSet<string>();

            foreach(var field in request.Fields)
            {
                fieldNames.Add(field.NameAsProperty);
                field.Id = ObjectId.GenerateNewId().ToString();
            }

            foreach (var relField in request.FieldsRelation)
            {
                fieldNames.Add(relField.NameAsProperty);
                relField.Id = ObjectId.GenerateNewId().ToString();
            }

            foreach (var name in  fieldNames)
            {
                if (!uniqueElements.Add(name))
                {
                    return BadRequest("There can't be two fields with the same name");
                }
                uniqueElements.Add(name);
            }

            var sessionOptions = new ClientSessionOptions { CausalConsistency = true };
            using (var session = await _divitiaeClient.StartSessionAsync(sessionOptions))
            {

                try
                {
                    string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                    User user = await _userServices.GetUserByEmail(result);

                    await _appServices.CanCreateApp(user.Id.ToString(), workspaceId);

                    Workspace ws = await _workspaceServices.GetWorkspaceById(workspaceId);

                    App app = new App() { AppName = request.AppName, AppIconId = request.AppIconId, Fields = request.Fields, RelationFields = request.FieldsRelation, WorkspaceId = ws.Id.ToString() };

                    await _appServices.InsertApp(app, session);
                    foreach(var field in app.Fields)
                    {
                        field.NameAsProperty = field.Name.ToLower().Replace(" ", "_");
                    }
                    foreach (var rField in app.RelationFields)
                    {
                        rField.NameAsProperty = rField.Name.ToLower().Replace(" ", "_");
                    }
                    WsAppsRelation wsAppsRelation = new WsAppsRelation
                    {
                        AppId = app.Id.ToString(),
                        Workspace = ws
                    };
                    ws.WsAppsRelations.Add(wsAppsRelation);
                    await _workspaceServices.UpdateWorkspace(ws);
                    AppDTO appDTO = new AppDTO()
                    {
                        Id = app.Id.ToString(),
                        AppName = app.AppName,
                        AppIconId = app.AppIconId,
                        Fields = app.Fields,
                        RelationFields = app.RelationFields
                    };
                    return Created("Created", appDTO);                    
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
        }

        /// <summary>
        /// Recibe una app y su ID. Se revisa que el usuario actual tiene permisos
        /// para hacer modificaciones en dicha app. Si es así, se actualiza la app
        /// en base de datos.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateApp([FromBody] App app, string id)
        {
            if (app == null) { return BadRequest(); }

            try
            {
                await UserCanModifyApp(id);

                await _appServices.UpdateApp(app);
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
        /// Recibe un string con el nombre de la app a actualizar. Se revisa
        /// que el usuario tiene permisos para modificar dicha app. Si es así,
        /// actualiza el nombre de la app en base de datos.
        /// </summary>
        /// <param name="appName"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPatch("{id}/edit-name")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateAppName([FromBody] string appName, string id)
        {
            if (app == null) { return BadRequest(); }

            try
            {
                await UserCanModifyApp(id);

                App app = await _appServices.GetAppById(id);
                app.AppName = appName;
                await _appServices.UpdateApp(app);
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
        /// Recibe una ID de una app. Revisa si el usuario actual puede modificar
        /// la app en cuestión. Si puede, elimina la app y sus tareas asociadas.
        /// </summary>
        /// <param name="id"></param>
        /// <returns>NoContent(204)</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteApp(string id)
        {
            try
            {
                await UserCanModifyApp(id);

                await _appServices.DeleteApp(id);

                List<AppTask> tasks = await _appTaskServices.GetAppTasksByApp(id);

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
        /// Revisa si el usuario actual puede modificar la app con ID appId.
        /// </summary>
        /// <param name="appId"></param>
        /// <returns></returns>
        private async Task UserCanModifyApp(string appId)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User user = await _userServices.GetUserByEmail(result);

            await _appServices.CanModifyApp(user.Id.ToString(), appId);
        }
    }
}
