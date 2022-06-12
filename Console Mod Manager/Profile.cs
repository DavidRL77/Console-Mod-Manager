using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace Console_Mod_Manager
{
    public class Profile
    {
        public string Name { get; set; }
        public string ModsPath { get; set; }
        public string UnusedModsPath { get; set; }
        public string ExecutablePath
        {
            get
            {
                if(executablePath == null) return "None";
                else return executablePath;
            }
            set { executablePath =  value; }
        }
        private string executablePath;

        public SortType SortBy { get; set; }
        public bool SortAscending { get; set; }
        public int Index { get; set; }

        public enum SortType 
        {
            Name,
            Date,
            Enabled
        };


        public Profile(string name, string modsPath, string unusedModsPath, int index, string executablePath = null, SortType sortBy = SortType.Name, bool sortAscending = true)
        {
            if(Path.GetFullPath(modsPath).Equals(Path.GetFullPath(unusedModsPath))) throw new Exception("The mods folder and unused mods folder cannot be the same");

            Name = name;
            ModsPath = modsPath;
            UnusedModsPath = unusedModsPath;
            Index = index;
            ExecutablePath = executablePath;
            SortBy = sortBy;
            SortAscending = sortAscending;
        }
        
        public void Save(string folderPath, JsonSerializerOptions options = null)
        {
            JsonDataManager.Save(this, Path.Join(folderPath, Name + ".json"), options);
        }

        public static Profile Load(string filePath, JsonSerializerOptions options = null)
        {
            return JsonDataManager.Load<Profile>(filePath, options);
        }

    }
}
