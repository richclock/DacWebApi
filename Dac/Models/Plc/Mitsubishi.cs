using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Dac.Interfaces;
using Dac.Models.Config;

using Microsoft.Extensions.Options;
using EasyModbus;

namespace Dac.Models.Plc
{
    public class Mitsubishi : IPlc
    {
        private PlcConfig _config = null;
        private bool _isConnected = false;
        private bool disposedValue;
        private int _mStart = 8192;
        private ModbusClient _modbusClient = null;

        public bool IsConnected => _isConnected;

        public Mitsubishi(IOptions<PlcConfig> config)
        {
            _config = config.Value;
        }
        #region 需實作區域
        public bool Connect()
        {
            try
            {
                string ip = _config.IP;
                int port = _config.Port;
                _modbusClient = new ModbusClient(ip, port);
                _modbusClient.Connect(ip, port);
                _isConnected = _modbusClient.Connected;
                return _isConnected;
            }
            catch (Exception ex)
            {
                return false;
            }

        }

        public bool Disconnect()
        {
            return true;
        }
        public bool ReadCoil(int address)
        {
            return true;
        }

        public int ReadHoldingReg(int address)
        {
            return _modbusClient.ReadHoldingRegisters(address, 1)[0];
        }

        public int ReadHoldingReg32bit(int address)
        {
            return 1;
        }

        public bool WriteCoil(int address, bool value)
        {
            return true;
        }

        public bool WriteHoldingReg32bit(int address, int value)
        {
            return true;
        }

        public bool WriteHoldingReg16bit(int address, int value)
        {
            return true;
        }
        #endregion
        #region Async Function
        public async Task<bool> ReadCoilAsync(int address)
        {
            return await Task.Run(() =>
            {
                return ReadCoil(address);
            });
        }

        public async Task<int> ReadHoldingRegAsync(int address)
        {
            return await Task.Run(() =>
            {
                return ReadHoldingReg(address);
            });
        }

        public async Task<int> ReadHoldingReg32bitAsync(int address)
        {
            return await Task.Run(() =>
            {
                return ReadHoldingReg32bit(address);
            });
        }

        public async Task<bool> WriteCoilAsync(int address, bool value)
        {
            return await Task.Run(() =>
            {
                return WriteCoil(address, value);
            });
        }

        public async Task<bool> WriteHoldingReg32bitAsync(int address, int value)
        {
            return await Task.Run(() =>
            {
                return WriteHoldingReg32bit(address, value);
            });
        }

        public async Task<bool> WriteHoldingReg16bitAsync(int address, int value)
        {
            return await Task.Run(() =>
            {
                return WriteHoldingReg16bit(address, value);
            });
        }

        public async Task<bool> ConnectAsync()
        {
            return await Task.Run(() =>
            {
                return Connect();
            });
        }

        public async Task<bool> DisconnectAsync()
        {
            return await Task.Run(() =>
            {
                return Connect();
            });
        }
        #endregion
        public void Dispose()
        {
            throw new NotImplementedException();
        }


    }
}
