using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

using Dac.Interfaces;
using Dac.Models.Config;

using Microsoft.Extensions.Options;

namespace Dac.Models.Plc
{
    public class TestPlc : IPlc
    {
        private IConfig _config = null;
        private bool _isConnected = false;
        private bool disposedValue;

        public bool IsConnected => _isConnected;

        public TestPlc(IOptions<PlcConfig> config)
        {
            _config = config.Value;
        }
        #region 需實作區域
        public bool Connect()
        {
            string ip = _config.IP;
            int port = _config.Port;
            _isConnected = true;
            return true;
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
            return 1;
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
        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: 處置受控狀態 (受控物件)
                }

                // TODO: 釋出非受控資源 (非受控物件) 並覆寫完成項
                // TODO: 將大型欄位設為 Null
                Disconnect();
                disposedValue = true;
            }
        }

        // // TODO: 僅有當 'Dispose(bool disposing)' 具有會釋出非受控資源的程式碼時，才覆寫完成項
        // ~TestPlc()
        // {
        //     // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // 請勿變更此程式碼。請將清除程式碼放入 'Dispose(bool disposing)' 方法
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
    }
}
