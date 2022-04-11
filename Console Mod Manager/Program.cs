using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;

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
        private bool canAbort = true;

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
            new Command("delete", "Deletes a profile", "delete <index>", C_DeleteProfile, "de", "d", "del"),
            new Command("change", "Changes the path of a folder in a profile", "edit mod/unused/exe <new_path>", C_EditProfile, "edit", "ch", "ed"),
            new Command("details", "Shows all the details of a profile", "details <index>", C_DetailsProfile, "see", "type", "detail"),
            new Command("enter", "Enters a profile", "enter <index>", C_EnterProfile, "en", "e"),
            new Command("rename", "Renames a profile", "rename\nrename <index> <new_name>", C_RenameProfile, "re", "r")
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

                Console.ForegroundColor = ConsoleColor.Gray;
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

                if(answer == "exit") break; //Self explanatory
                else if(string.IsNullOrEmpty(answer) || string.IsNullOrWhiteSpace(answer)) //If the command is empty, don't do anything
                {
                    Console.Clear();
                    continue;
                }

                try //Tries to execute the command
                {
                    Console.WriteLine();
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
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile = GetProfile(args[0]);
            int index = int.Parse(args[0]);

            ConsoleColor prevColor = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Cyan;
            //Asks for confirmation, if no, then cancel
            if(!YesNoAnswer($"Are you sure you want to delete '{profile.Name}'?"))
            {
                lastCommandOutput = "&rCancelled";
                Console.ForegroundColor = prevColor;
                return;
            }
            Console.ForegroundColor = prevColor;

            profiles.RemoveAt(index);
            lastCommandOutput = $"&gDeleted profile '{profile.Name}'";
            SaveProfiles();
        }

        public void C_EditProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            if(args.Length > 3) throw new Exception("Too many parameters");

            string parameter = "";
            string newPath = "";

            Profile profile = GetProfile(args[0]);

            if(args.Length >= 2) parameter = args[1];
            if(args.Length == 3) newPath = args[2];

            if(parameter == "")
            {
                ClearLine();
                Console.Write("Path to edit (mods/unused/exe): ");
                parameter = Console.ReadLine().Trim().ToLower();
            }

            string currentPath = "";

            //Checks if the parameter is valid
            if(parameter.Contains("unused")) 
            { 
                parameter = "unused";
                currentPath = profile.UnusedModsPath;
            }
            else if(parameter.Contains("mod")) 
            { 
                parameter = "mod";
                currentPath = profile.ModsPath;
            }
            else if(parameter.Contains("exe")) 
            { 
                parameter = "exe";
                currentPath = profile.ExecutablePath;
            }
            else throw new Exception("Invalid parameter, must be: mods/unused/exe");

            if(newPath == "")
            {
                ClearLine();
                Console.WriteLine($"\nCurrent path: {currentPath}");
                Console.Write("New path (empty to cancel): ");
                newPath = Console.ReadLine().Trim().Replace("\"", "");
                if(newPath == "")
                {
                    lastCommandOutput = "&rCancelled";
                    return;
                }
            }
            if(parameter == "exe")
            {
                if(!File.Exists(newPath)) throw new Exception($"File {newPath} does not exist");
            }
            else if(!Directory.Exists(newPath)) throw new Exception($"Directory {newPath} does not exist");

            //Sets the new path
            switch(parameter)
            {
                case "mod":
                    profile.ModsPath = newPath;
                    break;
                case "unused":
                    profile.UnusedModsPath = newPath;
                    break;
                case "exe":
                    profile.ExecutablePath = newPath;
                    break;
            }

            lastCommandOutput = $"&gEdited profile '{profile.Name}'";
        }

        public void C_DetailsProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile = GetProfile(args[0]);
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"{profile.Name}:");

            //Mods
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Mods Path: ");
            Console.ForegroundColor = prevColor;
            Console.WriteLine(profile.ModsPath);

            //Unused mods
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Unused Mods Path: ");
            Console.ForegroundColor = prevColor;
            Console.WriteLine(profile.UnusedModsPath);

            //Executable
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Executable Path: ");
            Console.ForegroundColor = prevColor;
            Console.WriteLine(profile.ExecutablePath);


            Console.ForegroundColor = prevColor;

            Console.Write("\n\n(Enter to continue)");
            Console.ReadLine();
        }

        public void C_EnterProfile(string[] args)
        {
            lastCommandOutput = "&mNot implemented";
        }

        public void C_RenameProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 2) throw new Exception("Too many arguments");

            Profile profile = GetProfile(args[0]);

            string name = args.Length == 2 ? args[1] : "";
            if(name == "")
            {
                ClearLine();
                Console.Write("Name: ");
                name = Console.ReadLine().Trim();
            }

            if(string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name)) throw new Exception("Name cannot be empty");
            profile.Name = name;
            lastCommandOutput = $"&gProfile renamed to '{profile.Name}'";
        }
        #endregion

        public Profile GetProfile(string index)
        {
            int i;
            if(int.TryParse(index, out i))
            {
                return GetProfile(i);
            }
            else
            {
                throw new Exception("Please enter a number");
            }
        }
        public Profile GetProfile(int index)
        {
            if(index >= profiles.Count) throw new Exception("Index out of range");
            else if(index < 0) throw new Exception("Index must be positive");
            return profiles[index];
        }

        public void CreateProfile(string name = "", string modsFolder = "", string unusedModsFolder = "", string executablePath = "")
        {
            //Name
            if(name == "")
            {
                ClearLine();
                Console.Write("Name: ");
                name = Console.ReadLine().Trim();

            }
            if(string.IsNullOrEmpty(name) || string.IsNullOrWhiteSpace(name)) throw new Exception("Name cannot be empty");

            //Mods folder
            if(modsFolder == "")
            {
                ClearLine();
                Console.Write("Mods folder: ");
                modsFolder = Console.ReadLine().Trim().Replace("\"", "");
            }
            if(!Directory.Exists(modsFolder)) throw new DirectoryNotFoundException($"Directory '{modsFolder}' does not exist");

            //Unused mods folder
            if(unusedModsFolder == "")
            {
                ClearLine();
                Console.Write("Unused mods folder: ");
                unusedModsFolder = Console.ReadLine().Trim().Replace("\"", "");

            }
            if(!Directory.Exists(unusedModsFolder)) throw new DirectoryNotFoundException($"Directory '{unusedModsFolder}' does not exist");
            if(Path.GetFullPath(modsFolder).Equals(Path.GetFullPath(unusedModsFolder))) throw new Exception("The mods folder and unused mods folder cannot be the same");

            //Executable
            if(executablePath == "")
            {
                ClearLine();
                Console.Write("Executable path (Empty for none): ");
                executablePath = Console.ReadLine().Trim().Replace("\"", "");
                if(executablePath == "") executablePath = null;
            }
            if(executablePath != null && !File.Exists(executablePath)) throw new FileNotFoundException($"File '{executablePath}' does not exist");

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
                Console.WriteLine("No profiles.");
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
            Console.WriteLine(profiles.Count == 0 ? "No profiles found" : "Loaded " + profiles.Count + " profiles.");
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

        public bool YesNoAnswer(string question)
        {
            Console.WriteLine(question + "(y/n)");
            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                if(key.KeyChar == 'y') { Console.WriteLine("y"); return true; }
                if(key.KeyChar == 'n') { Console.WriteLine("n"); return false; }
            } while(key.Key != ConsoleKey.Escape);

            Console.WriteLine("Esc");
            throw new Exception("Cancelled");
        }
    }
}
