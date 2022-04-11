using System;
using System.Text.Json;
using System.IO;
using System.Collections.Generic;

namespace Console_Mod_Manager
{
    internal class Program
    {
        public List<Profile> profiles;
        public JsonSerializerOptions defaultOptions = new JsonSerializerOptions { WriteIndented = true };

        //The paths
        public string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ModManager";
        public string profilesPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ModManager\\profiles.json";

        public CommandParser profileCommands;

        public string lastCommandOutput = "";

        static void Main(string[] args)
        {
           
            Program p = new Program();
            p.Init();
            p.Run();
        }

        public void Init()
        {
           profileCommands = new CommandParser(DisplayHelp,
           new Command("create", "Creates a new profile", "create\ncreate <name>\ncreate <name> <mods_folder> <unused_mods_folder>", C_CreateProfile, "cr", "c"),
           new Command("delete", "Deletes a profile", "delete <index>\ndelete <name>", C_DeleteProfile, "de", "d", "del"),
           new Command("enter", "Enters a profile", "enter <index>\nenter <name>", C_EnterProfile, "en", "e"),
           new Command("rename", "Renames a profile", "rename <old_name> <new_name>\nrename <index> <new_name>", C_RenameProfile, "re", "r")
           );

        }

        public void Run()
        {
            LoadProfiles();
            Console.WriteLine();
            ExecuteCommands(DisplayProfiles, profileCommands);
        }

        //Will execute commands on a loop until the user exits
        public void ExecuteCommands(Action startAction, CommandParser commandParser)
        {
            string answer;
            do
            {
                //Execute the start action
                startAction();
                
                //Displays all the commands
                Console.WriteLine($"Enter a command ({commandParser.ParseToString('/')}) or 'exit' to exit");

                //Before asking for user input, it displays the last command output
                if(lastCommandOutput != "")
                {
                    //Stores all the info before changing anything
                    int top = Console.CursorTop;
                    ConsoleColor prevColor = Console.ForegroundColor;


                    //Determins what color the output should be
                    bool colored = true;
                    if(lastCommandOutput.StartsWith("&r")) Console.ForegroundColor = ConsoleColor.Red;
                    else if(lastCommandOutput.StartsWith("&g")) Console.ForegroundColor = ConsoleColor.Green;
                    else if(lastCommandOutput.StartsWith("&b")) Console.ForegroundColor = ConsoleColor.Blue;
                    else if(lastCommandOutput.StartsWith("&y")) Console.ForegroundColor = ConsoleColor.Yellow;
                    else if(lastCommandOutput.StartsWith("&c")) Console.ForegroundColor = ConsoleColor.Cyan;
                    else if(lastCommandOutput.StartsWith("&m")) Console.ForegroundColor = ConsoleColor.Magenta;
                    else colored = false;

                    if(colored) lastCommandOutput = lastCommandOutput[2..];

                    Console.WriteLine("\n\n" + lastCommandOutput);
                    lastCommandOutput = "";
                    Console.CursorTop = top;

                    Console.ForegroundColor = prevColor;
                }

                //Asks for user input
                answer = Console.ReadLine();

                //Self explanatory
                if(answer == "exit") break;
                
                
                try //Tries to execute the command
                {
                    commandParser.TryExecute(answer);
                }
                catch(Exception e) //If it fails, it saves the error message in red
                {
                    lastCommandOutput = "&rError: " + e.Message;
                }

                Console.Clear();
                
            } while(answer != "exit");
        }

        #region Commands
        public void C_CreateProfile(string[] args)
        {
            lastCommandOutput = "&gProfile created";
            switch(args.Length)
            {
                case 0:
                    CreateProfile();
                    break;
                case 1:
                    CreateProfile(args[0]);
                    break;
                case 2:
                    CreateProfile(args[0], args[1]);
                    break;
                case 3:
                    CreateProfile(args[0], args[1], args[2]);
                    break;
                case 4:
                    CreateProfile(args[0], args[1], args[2], args[3]);
                    break;
                default:
                    lastCommandOutput = "";
                    throw new Exception("Too many arguments");
                    break;
            }
        }

        public void C_DeleteProfile(string[] args)
        {

        }

        public void C_EnterProfile(string[] args)
        {

        }

        public void C_RenameProfile(string[] args)
        {

        }
        #endregion

        public void CreateProfile(string name = "", string modsFolder = "", string unusedModsFolder = "", string executablePath = "")
        {
            Console.WriteLine();
            if(name == "")
            {
                do
                {
                    ClearLine();
                    Console.Write("Name: ");
                    name = Console.ReadLine().Trim();

                    if(string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(name))
                    {
                        Console.Write("Name cannot be empty");
                        Console.CursorTop -= 1;
                    }
                }
                while(string.IsNullOrWhiteSpace(name) || string.IsNullOrEmpty(name));

            }
            if(string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name)) throw new Exception("Name cannot be empty");

            if(modsFolder == "")
            {
                do
                {
                    ClearLine();
                    Console.Write("Mods folder: ");
                    modsFolder = Console.ReadLine().Trim().Replace("\"", "");
                    
                    if(!Directory.Exists(modsFolder))
                    {
                        Console.Write("Folder does not exist");
                        Console.CursorTop -= 1;
                    }
                }
                while(!Directory.Exists(modsFolder));
               
            }
            if(!Directory.Exists(modsFolder)) throw new DirectoryNotFoundException($"Directory '{modsFolder}' does not exist");
            
            if(unusedModsFolder == "")
            {
                do
                {
                    ClearLine();
                    Console.Write("Unused mods folder: ");
                    unusedModsFolder = Console.ReadLine().Trim().Replace("\"", "");

                    if(!Directory.Exists(unusedModsFolder))
                    {
                        Console.Write("Folder does not exist");
                        Console.CursorTop -= 1;
                    }
                }
                while(!Directory.Exists(unusedModsFolder));
            }
            if(!Directory.Exists(unusedModsFolder)) throw new DirectoryNotFoundException($"Directory '{unusedModsFolder}' does not exist");
            
            if(executablePath == "")
            {
                do
                {
                    ClearLine();
                    Console.Write("Executable path (Empty for none): ");
                    executablePath = Console.ReadLine().Trim().Replace("\"", "");
                    if(executablePath == "") { executablePath = null; break; }

                    if(!File.Exists(executablePath))
                    {
                        Console.Write("File does not exist");
                        Console.CursorTop -= 1;
                    }
                }
                while(!File.Exists(executablePath));
               
            }
            if(!File.Exists(executablePath)) throw new FileNotFoundException($"File '{executablePath}' does not exist");

            profiles.Add(new Profile(name, modsFolder, unusedModsFolder, executablePath));
            SaveProfiles();
        }
        
        public void DisplayProfiles()
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Profiles:");

            if(profiles.Count == 0)
            {
                Console.WriteLine("0 profiles.");
                Console.WriteLine();
                return;
            }

            for(int i = 0; i < profiles.Count; i++)
            {
                Console.WriteLine($"{i}.- {profiles[i].Name}");
            }

            Console.ForegroundColor = prevColor;
            Console.WriteLine();
        }
        public void LoadProfiles() 
        {
            Console.WriteLine("Loading profiles...");
            string message = "";
            
            if(!File.Exists(profilesPath))
            {
                profiles = new List<Profile>();
            }
            else
            {
                try
                {
                    profiles = JsonDataManager.Load<List<Profile>>(profilesPath);
                    if(profiles == null) profiles = new List<Profile>();
                }
                catch(Exception e)
                {
                    profiles = new List<Profile>();
                    message = "Error while loading profiles: " + e.Message;
                }
               
            }

            Console.Clear();
            if(message != "") Console.WriteLine(message);
            Console.WriteLine(profiles.Count == 0? "No profiles loaded":"Loaded " + profiles.Count + " profiles.");
        }
        public void SaveProfiles() 
        {
            try
            {
                JsonDataManager.Save(profiles, profilesPath, defaultOptions);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error while saving profiles: " + e.Message);
            }
        }
        public void ClearLine()
        {
            int top = Console.CursorTop;
            Console.SetCursorPosition(0, top);
            Console.Write(new string(' ', Console.WindowWidth));
            Console.SetCursorPosition(0, top);
        }

        public void DisplayHelp(string help)
        {
            ConsoleColor prevColor = Console.ForegroundColor; 

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("\n" + help);
            
            Console.ForegroundColor = prevColor;
            Console.Write("\n(Enter to continue)");
            Console.ReadLine();
        }
    }
}
