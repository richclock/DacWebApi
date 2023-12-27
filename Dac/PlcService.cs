using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;

using Dac.Interfaces;
using Dac.Models.Config;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Dac
{
    public class PlcService : IPlcService
    {
        #region Private Field
        private readonly IPlc _plc;
        private readonly ILogger _logger;
        private PlcConfig _config = null;
        private bool disposedValue;
        private System.Threading.Timer _timer = null;
        #endregion
        #region Public Field
        public bool IsConnected => _plc.IsConnected;
        #endregion
        public PlcService(IPlc plc, ILogger<IPlcService> logger)
        {
            _plc = plc;
            _logger = logger;
        }



        protected virtual void Dispose(bool disposing)
        {
            _plc.Dispose();
            _timer?.Dispose();
        }
        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }


        #region DI Function
        public bool ReadCoil(int address)
        {
            return _plc.ReadCoil(address);
        }

        public int ReadHoldingReg(int address)
        {
            return _plc.ReadHoldingReg(address);
        }

        public int ReadHoldingReg32bit(int address)
        {
            return _plc.ReadHoldingReg32bit(address);
        }

        public bool WriteCoil(int address, bool value)
        {
            return _plc.WriteCoil(address, value);
        }

        public bool WriteHoldingReg32bit(int address, int value)
        {
            return _plc.WriteHoldingReg32bit(address, value);
        }

        public bool WriteHoldingReg16bit(int address, int value)
        {
            return _plc.WriteHoldingReg16bit(address, value);
        }

        public async Task<bool> ReadCoilAsync(int address)
        {
            return await _plc.ReadCoilAsync(address);
        }

        public async Task<int> ReadHoldingRegAsync(int address)
        {
            return await _plc.ReadHoldingRegAsync(address);
        }

        public async Task<int> ReadHoldingReg32bitAsync(int address)
        {
            return await _plc.ReadHoldingReg32bitAsync(address);
        }

        public async Task<bool> WriteCoilAsync(int address, bool value)
        {
            return await _plc.WriteCoilAsync(address, value);
        }

        public async Task<bool> WriteHoldingReg32bitAsync(int address, int value)
        {
            return await _plc.WriteHoldingReg32bitAsync(address, value);
        }

        public async Task<bool> WriteHoldingReg16bitAsync(int address, int value)
        {
            return await _plc.WriteHoldingReg16bitAsync(address, value);
        }

        public bool Connect()
        {
            return _plc.Connect();
        }

        public bool Disconnect()
        {
            return _plc.Disconnect();
        }

        public async Task<bool> ConnectAsync()
        {
            return await _plc.ConnectAsync();
        }

        public async Task<bool> DisconnectAsync()
        {
            return await _plc.DisconnectAsync();
        }


        #endregion



    }
}
