using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dac.Interfaces
{
    public interface IEquipment : IDisposable
    {
        bool IsConnected { get; }
        bool Connect();
        bool Disconnect();
        Task<bool> ConnectAsync();
        Task<bool> DisconnectAsync();

    }
}
