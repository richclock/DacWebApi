using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Dac.Interfaces
{
    public interface IConfig
    {
        string Name { get; set; }
        string Brand { get; set; }
        string IP { get; set; }
        int Port { get; set; }
    }
}
