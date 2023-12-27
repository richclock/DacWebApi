using Dac.Interfaces;

using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace DacWebApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [EnableCors("Cors_Policy")]
    public class PlcController : Controller
    {
        private readonly ILogger _logger;
        private readonly IPlcService _plcSvc;

        public PlcController(IPlcService plcSvc, ILogger<PlcController> logger)
        {
            _plcSvc = plcSvc;
            _logger = logger;
        }
        [HttpGet("Connect")]
        public async Task<ActionResult<ApiResponse>> Connect()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse.Create(null, false, "Invalid Request"));
                }

                var isConnected = await _plcSvc.ConnectAsync();

                return Ok(ApiResponse.Create(isConnected, true));
            }
            catch (Exception ex)
            {
                _logger.LogError("發生錯誤 : " + ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Create(null, false, ex.Message));
            }
        }
        [HttpGet("Disconnect")]
        public async Task<ActionResult<ApiResponse>> Disconnect()
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse.Create(null, false, "Invalid Request"));
                }

                var isConnected = await _plcSvc.DisconnectAsync();

                return Ok(ApiResponse.Create(isConnected, true));
            }
            catch (Exception ex)
            {
                _logger.LogError("發生錯誤 : " + ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Create(null, false, ex.Message));
            }
        }
        [HttpPost("ReadHoldingReg")]
        public async Task<ActionResult<ApiResponse>> ReadHoldingReg([FromBody] int address)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ApiResponse.Create(null, false, "Invalid Request"));
                }

                if (!_plcSvc.IsConnected)
                {
                    return Ok(ApiResponse.Create(null, false, $"PLC尚未連線"));
                }

                var value = await _plcSvc.ReadHoldingRegAsync(address);

                return Ok(ApiResponse.Create(value, true));
            }
            catch (Exception ex)
            {
                _logger.LogError("發生錯誤 : " + ex.Message);
                return StatusCode(StatusCodes.Status500InternalServerError, ApiResponse.Create(null, false, ex.Message));
            }
        }
    }
}

