using divitiae_api.Models;
using divitiae_api.Models.DTOs;
using divitiae_api.Models.Exceptions;
using divitiae_api.Services;
using divitiae_api.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json.Nodes;

namespace divitiae_api.Controllers
{
    [Route("api/app/{appId}/[controller]")]
    [ApiController]
    public class ItemController : ControllerBase
    {
        private readonly IItemServices _itemServices;
        private readonly IAppServices _appServices;
        private readonly IAppTaskServices _appTaskServices;
        private readonly IUserServices _userServices;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContentAccessor;


        public ItemController(IItemServices itemServices, IConfiguration configuration, IAppServices appServices, IUserServices userServices, IHttpContextAccessor httpContentAccessor, IAppTaskServices appTaskServices)
        {
            _itemServices = itemServices;
            _configuration = configuration;
            _appServices = appServices;
            _userServices = userServices;
            _httpContentAccessor = httpContentAccessor;
            _appTaskServices = appTaskServices;
        }

        /// <summary>
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así,
        /// devuelve una lista de todos los items de dicha app.
        /// </summary>
        /// <param name="appId"></param>
        /// <returns>Lista de ItemDTO</returns>
        [HttpGet("all")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAppItems(string appId)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                return Ok(await _itemServices.GetAppItems(appId));
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
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así,
        /// devuelve una lista de Items en orden ascendente o descendente según indique
        /// el parámetro ascending y con el offset indicado en el parámetro con el mismo nombre.
        /// </summary>
        /// <param name="offset"></param>
        /// <param name="idSort"></param>
        /// <param name="ascending"></param>
        /// <param name="appId"></param>
        /// <returns>Lista de ItemDTO</returns>
        [HttpGet("paginated")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAppItemsPaginated([FromQuery] int offset, [FromQuery] string? idSort, [FromQuery] bool ascending, string appId)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                return Ok(await _itemServices.GetAppItemsPaginated(appId, offset, ascending));
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
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así,
        /// devuelve un itemDTO del item con ID id.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="id"></param>
        /// <returns>ItemDTO</returns>
        [HttpGet("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAppItemById(string appId, string id)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                Item item = await _itemServices.GetAppItemById(id, appId);

                if (item is null)
                {
                    return NotFound();
                }

                ItemDTO itemDTO = new ItemDTO()
                {
                    Id = id,
                    DescriptiveName = item.DescriptiveName,
                    FieldsValue = item.FieldsValue,
                    FieldsRelationValue = item.FieldsRelationValue,
                    Relations = item.Relations
                };

                return Ok(itemDTO);
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
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así,
        /// busca en base de datos todos los items de dicha app cuyo descriptiveName 
        /// contenga el texto que trae el parámetro dName.
        /// </summary>
        /// <param name="dName"></param>
        /// <param name="appId"></param>
        /// <returns>Lista de ItemDTO</returns>
        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetAppItemsFiltered([FromQuery] string dName, string appId)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                List<ItemDTO> items = await _itemServices.GetAppItemsByName(dName, appId);

                if (items is null)
                {
                    return NotFound();
                }

                return Ok(items);
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
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así, revisa
        /// que el itemDTOCreate que recibe es correcto y que tiene la misma estructura que la app
        /// a la que pertenece. Si es correcto, inserta el item en base de datos y devuelve un itemDTO
        /// del mismo.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="itemDTO"></param>
        /// <returns>ItemDTO</returns>
        [HttpPost("add")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateItem(string appId, [FromBody] ItemDTOCreate itemDTO)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                if (itemDTO == null) { return BadRequest("Item is null"); }

                App app = await _appServices.GetAppById(appId);
                bool allFieldsMatched = (app.Fields == null && itemDTO.FieldsValue == null) ||
                    (app.Fields != null && itemDTO.FieldsValue != null && app.Fields.All(fs =>
                        itemDTO.FieldsValue.Any(fv =>
                            fv.Name == fs.Name &&
                            fv.Type == fs.Type &&
                            fv.NameAsProperty == fs.NameAsProperty
                        )
                    ));
                bool allRelationFieldsMatched = (app.RelationFields == null && itemDTO.FieldsRelationValue == null) ||
                    (app.RelationFields != null && itemDTO.FieldsRelationValue != null && app.RelationFields.All(fs =>
                        itemDTO.FieldsRelationValue.Any(fv =>
                            fv.Name == fs.Name &&
                            fv.Type == fs.Type &&
                            fv.NameAsProperty == fs.NameAsProperty
                        )
                    ));
            

                if (!allFieldsMatched || !allRelationFieldsMatched)
                    return BadRequest("Wrong Item structure. Should be equal to its app's structure");


                foreach(FieldValue fv in itemDTO.FieldsValue)
                {

                    switch (fv.Type)
                    {
                        case "string":
                            fv.Value = fv.Value.ToString(); 
                            break;
                        case "int":
                            fv.Value = double.Parse(fv.Value.ToString(), CultureInfo.InvariantCulture);
                            break;
                        case "decimal":
                        case "currency":
                            fv.Value = double.Parse(fv.Value.ToString(), CultureInfo.InvariantCulture);
                            break;
                        case "date":
                        case "datetime":
                            fv.Value = int.Parse(fv.Value.ToString(), CultureInfo.InvariantCulture);
                            break;
                        case "boolean":
                            fv.Value = Convert.ToBoolean(fv.Value.ToString());
                            break;
                    }
                }

            

                Item item = new()
                {
                    DescriptiveName = itemDTO.DescriptiveName,
                    FieldsRelationValue = itemDTO.FieldsRelationValue,
                    FieldsValue = itemDTO.FieldsValue
                };

                await _itemServices.InsertItem(item, appId);

                ItemDTO itemDTO1 = new ItemDTO()
                {
                    Id = item.Id.ToString(),
                    DescriptiveName = item.DescriptiveName,
                    FieldsValue = item.FieldsValue,
                    FieldsRelationValue = item.FieldsRelationValue,
                    Relations = item.Relations
                };

                return Created("Created", itemDTO1);
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
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así, revisa que
        /// el itemDTO que recibe tiene la estructura correcta. Si todo está correcto, actualiza el item
        /// con ID id en base de datos.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="item"></param>
        /// <param name="id"></param>
        /// <returns>Created(201)</returns>
        [HttpPut("{id}")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> UpdateItem(string appId, [FromBody] ItemDTO item, string id)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                if (item == null) { return BadRequest(); }

                App app = await _appServices.GetAppById(appId);

                bool allFieldsMatched = app.Fields.All(fs =>
                    item.FieldsValue.Any(fv =>
                        fv.Name == fs.Name &&
                        fv.Type == fs.Type &&
                        fv.NameAsProperty == fs.NameAsProperty
                    )
                );
                bool allRelationFieldsMatched = app.RelationFields.All(fs =>
                    item.FieldsRelationValue.Any(fv =>
                        fv.Name == fs.Name &&
                        fv.Type == fs.Type &&
                        fv.NameAsProperty == fs.NameAsProperty
                    )
                );

                if (!allFieldsMatched || !allRelationFieldsMatched)
                {
                    return BadRequest("Wrong Item structure. Should be equal to its app's structure");
                }

                foreach (FieldValue fv in item.FieldsValue)
                {

                    switch (fv.Type)
                    {
                        case "string":
                            fv.Value = fv.Value.ToString();
                            break;
                        case "int":
                            fv.Value = int.Parse(fv.Value);
                            break;
                        case "decimal":
                        case "currency":
                            fv.Value = decimal.Parse(fv.Value.ToString(), CultureInfo.InvariantCulture);
                            break;
                        case "date":
                        case "datetime":
                            fv.Value = int.Parse(fv.Value.ToString(), CultureInfo.InvariantCulture);
                            break;
                        case "boolean":
                            fv.Value = Convert.ToBoolean(fv.Value.ToString());
                            break;
                    }
                }

                Item item1 = new Item()
                {
                    Id = new MongoDB.Bson.ObjectId(id),
                    DescriptiveName = item.DescriptiveName,
                    FieldsValue = item.FieldsValue,
                    FieldsRelationValue = item.FieldsRelationValue,
                    Relations = item.Relations
                };
                await _itemServices.UpdateItem(item1, appId);

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

        /// <summary>
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así, 
        /// elimina el item con ID id en base de datos.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="id"></param>
        /// <returns>NoContent(204)</returns>
        [HttpDelete("{id}")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteItem(string appId, string id)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                await _itemServices.DeleteItem(appId, id);

                List<AppTask> tasks = await _appTaskServices.GetAppTasksByItem(id);

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

        }

        /// <summary>
        /// Revisa si el usuario actual puede acceder a la app con ID appId. Si es así, elimina
        /// todos los items cuyos IDs se encuentren en la lista itemsIds en base de datos.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="itemsIds"></param>
        /// <returns></returns>
        [HttpDelete("bulk-delete")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> DeleteItem(string appId, [FromBody] List<string> itemsIds)
        {
            try
            {
                string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
                User user = await _userServices.GetUserByEmail(result);

                await _appServices.CanAccessApp(user.Id.ToString(), appId);

                await _itemServices.BulkDelete(appId, itemsIds);

                foreach (var id in itemsIds)
                {
                    List<AppTask> tasks = await _appTaskServices.GetAppTasksByItem(id);

                    foreach (AppTask task in tasks)
                    {
                        await _appTaskServices.DeleteAppTask(task.Id.ToString());
                    }
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
        }
    }
}
