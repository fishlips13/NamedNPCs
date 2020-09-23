using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NamedNPCs
{
    public class Chatter
    {
        public string name;

        public int levelSpawns = 0;
        public int runSpawns = 0;

        public Chatter(string name)
        {
            this.name = name;
        }
    }
}
