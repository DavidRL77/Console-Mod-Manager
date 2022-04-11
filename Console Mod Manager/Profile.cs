using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Console_Mod_Manager
{
    public class Profile
    {
        public string Name { get; set; }
        public string ModsPath { get; set; }
        public string UnusedModsPath { get; set; }
        public string ExecutablePath { get; set; }


        public Profile(string name, string modsPath, string unusedModsPath, string executablePath = null)
        {
            Name = name;
            ModsPath = modsPath;
            UnusedModsPath = unusedModsPath;
            ExecutablePath = executablePath;
        }
       
    }
}
