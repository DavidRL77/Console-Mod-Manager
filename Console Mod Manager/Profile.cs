using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

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
            if(Path.GetFullPath(modsPath).Equals(Path.GetFullPath(unusedModsPath))) throw new Exception("The mods folder and unused mods folder cannot be the same");

            Name = name;
            ModsPath = modsPath;
            UnusedModsPath = unusedModsPath;
            ExecutablePath = executablePath;
        }
       
    }
}
