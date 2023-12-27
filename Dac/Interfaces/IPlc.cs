using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dac.Interfaces
{
    public interface IPlc : IEquipment
    {
        bool ReadCoil(int address);
        int ReadHoldingReg(int address);
        int ReadHoldingReg32bit(int address);
        bool WriteCoil(int address, bool value);
        bool WriteHoldingReg32bit(int address, int value);
        bool WriteHoldingReg16bit(int address, int value);
        Task<bool> ReadCoilAsync(int address);
        Task<int> ReadHoldingRegAsync(int address);
        Task<int> ReadHoldingReg32bitAsync(int address);
        Task<bool> WriteCoilAsync(int address, bool value);
        Task<bool> WriteHoldingReg32bitAsync(int address, int value);
        Task<bool> WriteHoldingReg16bitAsync(int address, int value);
    }
}
