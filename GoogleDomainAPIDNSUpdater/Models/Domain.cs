using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDomainAPIDNSUpdater.Models
{
    public class Domain
    {
        public Domain()
        {
            IP = string.Empty;
            Name = string.Empty;
        }

        public Domain(string ip, string name)
        {
            IP = ip;
            Name = name;
        }

        public string IP { get; set; }
        public string Name { get; set; }
    }
}
