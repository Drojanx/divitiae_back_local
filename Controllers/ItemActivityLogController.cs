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
    [Route("api/app/{appId}/item/{itemId}/[controller]")]
    [ApiController]
    public class ItemActivityLogController : ControllerBase
    {
        private readonly IItemServices _itemServices;
        private readonly IAppServices _appServices;
        private readonly IUserServices _userServices;
        private readonly IItemActivityLogServices _itemActivityLogServices;
        private readonly IConfiguration _configuration;
        private readonly IHttpContextAccessor _httpContentAccessor;


        public ItemActivityLogController(IItemServices itemServices, IConfiguration configuration, IItemActivityLogServices itemActivityLogServices, IUserServices userServices, IAppServices appServices, IHttpContextAccessor httpContentAccessor)
        {
            _itemServices = itemServices;
            _configuration = configuration;
            _itemActivityLogServices = itemActivityLogServices;
            _userServices = userServices;
            _appServices = appServices;
            _httpContentAccessor = httpContentAccessor;
        }

        /// <summary>
        /// Recibe los IDs de una app y un item. Revisa si el usaurio actual puede acceder a 
        /// dicha app. Si es así, devuelve los itemActivityLogs del item.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="itemId"></param>
        /// <returns>Lista de ItemActivityLog</returns>
        [HttpGet()]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> GetItemLogs(string appId, string itemId)
        {
            try
            {
                await UserCanAccessApp(appId);
                Item item = await _itemServices.GetAppItemById(itemId, appId);

                List<ItemActivityLog> logs = await _itemActivityLogServices.GetItemLogs(itemId);

                return Ok(logs);
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
            
        }

        /// <summary>
        /// Recibe los IDs de una app y un item, además de un itemActivityLogDTO. Revisa si el usaurio 
        /// actual y el indicado como Creator del log pueden acceder a dicha app. Si es así, inserta
        /// el itemActivityLog en base de datos.
        /// </summary>
        /// <param name="appId"></param>
        /// <param name="itemId"></param>
        /// <param name="log"></param>
        /// <returns>ItemActivityLog</returns>
        [HttpPost("add")]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<IActionResult> CreateItemActivityLog(string appId, string itemId, [FromBody] ItemActivityLogDTO log)
        {
            try
            {
                await _appServices.CanAccessApp(log.CreatorId, appId);
                await UserCanAccessApp(appId);

                Item item = await _itemServices.GetAppItemById(itemId, appId);


                ItemActivityLog iLog = new ItemActivityLog()
                {
                    ItemId = itemId,
                    AppId = appId,
                    CreatorId = log.CreatorId,
                    UnixCreatedOn = log.UnixCreatedOn,
                    LogText = log.LogText
                };
                
                return Ok(await _itemActivityLogServices.InsertItemActivityLog(iLog));
            }
            catch (ItemNotFoundException ex)
            {
                return NotFound(ex.Message);
            }
        }

        private async Task UserCanAccessApp(string appId)
        {
            string result = _httpContentAccessor.HttpContext.User.FindFirstValue(ClaimTypes.Email);
            User user = await _userServices.GetUserByEmail(result);

            await _appServices.CanAccessApp(user.Id.ToString(), appId);
        }

    }

    
}
