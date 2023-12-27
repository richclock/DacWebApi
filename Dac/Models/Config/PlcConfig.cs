using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Dac.Interfaces;

namespace Dac.Models.Config
{
    public class PlcConfig : IConfig
    {
        public string Name { get; set; }
        public string Brand { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
    }
}
