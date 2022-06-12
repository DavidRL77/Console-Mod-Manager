using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using static Console_Mod_Manager.Utilities;

namespace Console_Mod_Manager
{
    internal class Program
    {
        public List<Profile> profiles;
        public JsonSerializerOptions defaultOptions = new JsonSerializerOptions { WriteIndented = true };

        //The paths
        public string appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ModManager";
        public string profilesFolder = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData) + "\\ModManager\\profiles";

        public CommandParser profileCommands;
        public CommandParser modCommands;

        private Profile currentProfile;

        public string lastCommandOutput = "";
        public FileSystemInfo[] allMods;
        public List<FileSystemInfo> filteredMods;

        private string currentFilter = "";
        public MenuType currentMenu;

        public enum MenuType { Profiles, Mods };

        static void Main(string[] args)
        {
            Program p = new Program();
            p.Init();
            p.Run();
        }

        public void Init()
        {
            filteredMods = new List<FileSystemInfo>();
            Command filterCommand = new Command("filter", "Filters the items that are displayed. Empty to clear filter.", "filter <filter>\nEach word will be evaluated separately. The filter evaluates wether the name contains that word, or the opposite in the case of '-' in front.\n\nExample:\nfilter banana -apple\nOnly the names containing banana, and not containing apple will be shown.", C_Filter, "f", "search");

            profileCommands = new CommandParser(helpAction: DisplayHelp, indexAction: EnterIndexProfile,
                new Command("create", "Creates a new profile", "create <name> <mods_folder> <unused_mods_folder>", C_CreateProfile, "cr", "c"),
                new Command("delete", "Deletes a profile", "delete <index>", C_DeleteProfile, "de", "d", "del", "remove"),
                new Command("edit", "Edits the path of a folder in a profile", "change mod/unused/exe <new_path>", C_EditProfile, "change", "ch", "ed"),
                new Command("details", "Shows all the details of a profile", "details <index>", C_DetailsProfile, "see", "type", "detail"),
                new Command("load", "Loads a profile", "load <index>", C_EnterProfile, "enter", "en"),
                new Command("rename", "Renames a profile", "rename <index> <new_name>", C_RenameProfile, "re", "r"),
                new Command("move", "Moves a profile", "move <index> <new_index>", C_MoveProfile, "position"),
                filterCommand
                );

            modCommands = new CommandParser(helpAction: DisplayHelp, indexAction: AutoToggleMod,
                new Command("toggle", "Toggles a mod", "toggle <index>", C_ToggleMod, "togle", "t"),
                new Command("enable", "Enables a mod", "enable <index>", C_EnableMod, "e", "en"),
                new Command("disable", "Disables a mod", "disable <index>", C_DisableMod, "dis", "d"),
                new Command("delete", "Deletes a mod forever", "delete <index>", C_DeleteMod, "del", "remove", "de"),
                new Command("rename", "Renames a mod", "rename <index> <new_name>", C_RenameMod, "re", "r"),
                new Command("open", "Opens the directory of a mod or profile folder", "open mod/unused/exe/<index>", C_Open, "op", "go"),
                new Command("sort", "Sorts the mods by the option specified", "sort name/date/enabled [descending]", C_Sort, "order", "orderby", "sortby"),
                filterCommand
                );
        }

        public void Run()
        {
            LoadProfiles();
            Console.WriteLine();
            ExecuteCommands(startAction: DisplayProfiles, errorAction: HandleCommandError, profileCommands);
        }

        //Will execute commands on a loop until the user exits
        public void ExecuteCommands(Action startAction, Action<string, Exception> errorAction, CommandParser commandParser)
        {
            string answer;
            do
            {
                //Execute the start action
                startAction?.Invoke();

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
                    ClearLine();
                    commandParser.TryExecute(answer);
                }
                catch(Exception e) //If it fails, it saves the error message in red
                {
                    try
                    {
                        errorAction?.Invoke(answer, e);
                    }
                    catch(Exception e2)
                    {
                        lastCommandOutput = "&rError: " + e2.Message;
                    }
                }

                Console.Clear();

            } while(answer != "exit");
        }

        public void HandleCommandError(string command, Exception e)
        {
            if(e.GetType() == typeof(InvalidCommandException))
            {
                try
                {
                    if(currentMenu == MenuType.Profiles) profileCommands.IndexAction(profiles.GetIndexFromString(command, p => p.Name));
                    else if(currentMenu == MenuType.Mods) modCommands.IndexAction(allMods.GetIndexFromString(command, m => m.Name));
                }
                catch
                {
                    lastCommandOutput = "&rError: " + e.Message;
                }
            }
            else lastCommandOutput = "&rError: " + e.Message;

        }

        public void C_Filter(string[] args)
        {
            if(args.Length == 0) currentFilter = "";
            else currentFilter = string.Join(' ', args).Trim();

            if(currentFilter == "") lastCommandOutput = "&gRemoved filter";
            else lastCommandOutput = $"&gApplied filter '{currentFilter}'";
        }

        #region Profile Commands
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
                   
            }
        }

        public void C_DeleteProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile = profiles.GetElement(args[0], p => p.Name);
            int index = int.Parse(args[0]);

            //Asks for confirmation, if no, then cancel
            if(!YesNoAnswer($"Are you sure you want to delete '{profile.Name}'?"))
            {
                lastCommandOutput = "&rCancelled";
                return;
            }

            profiles.RemoveAt(index);
            lastCommandOutput = $"&gDeleted profile '{profile.Name}'";
            SaveProfiles();
        }

        public void C_EditProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            if(args.Length > 4) throw new Exception("Too many parameters");

            string parameter = "";
            string newPath = "";

            Profile profile = profiles.GetElement(args[0], p => p.Name);

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
            profile.Save(profilesFolder, defaultOptions);
        }

        public void C_DetailsProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile =  profiles.GetElement(args[0], p => p.Name);

            DisplayDetails(profile);

            Console.Write("\n\n(Enter to continue)");
            Console.ReadLine();
        }

        public void C_EnterProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile =  profiles.GetElement(args[0], p => p.Name);
            LoadProfile(profile);
        }

        public void C_RenameProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 2) throw new Exception("Too many arguments");

            Profile profile =  profiles.GetElement(args[0], p => p.Name);

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
            profile.Save(profilesFolder, defaultOptions);
        }

        public void C_MoveProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 2) throw new Exception("Too many arguments");

            Profile profile =  profiles.GetElement(args[0], p => p.Name);
            Profile profile2 = profiles.GetElement(args[1], p => p.Name);

            int index = profiles.IndexOf(profile);
            int index2 = profiles.IndexOf(profile2);

            if(index == index2) throw new Exception("Cannot move profile to itself");

            profile.Index = index2;
            profiles.RemoveAt(index);
            profiles.Insert(index2, profile);
            SaveProfiles();
            SortProfiles();
            lastCommandOutput = $"&gMoved profile '{profile.Name}' to index {index2}";
        }
        #endregion

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

            if(profiles.Any(p => p.Name.ToLower() == name.ToLower())) throw new Exception("Profile with that name already exists");

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

            profiles.Add(new Profile(name, modsFolder, unusedModsFolder, profiles.Count, executablePath));
            SaveProfiles();
        }

        public void DisplayDetails(Profile profile)
        {
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

            //Sort Type
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write("Sort By: ");
            Console.ForegroundColor = prevColor;
            Console.WriteLine(profile.SortBy + " - " + (profile.SortAscending ? "Ascending" : "Descending"));

            Console.ForegroundColor = prevColor;
        }

        public void DisplayProfiles()
        {
            currentMenu = MenuType.Profiles;
            ConsoleColor prevColor = Console.ForegroundColor;


            //Shows the current filter
            Console.ForegroundColor = ConsoleColor.Magenta;
            if(currentFilter != "") Console.WriteLine($"Filter: {currentFilter}");
            Console.ForegroundColor = prevColor;

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine($"Profiles:");

            if(profiles.Count == 0)
            {
                Console.WriteLine("No profiles.");
                Console.WriteLine();
                return;
            }

            for(int i = 0; i < profiles.Count; i++)
            {
                //The string that will be checked against the filter
                string filterString = profiles[i].Name;
                if(!PassesFilter(filterString, currentFilter)) continue;

                Console.WriteLine($"{i}.- {profiles[i].Name}");
            }

            Console.ForegroundColor = prevColor;
            Console.WriteLine();
        }
        
        public void LoadProfiles()
        {
            Console.WriteLine("Loading profiles...");
            string message = "";

            profiles = new List<Profile>();
            if(!Directory.Exists(profilesFolder))
            {
                Directory.CreateDirectory(profilesFolder);
            }
            else
            {
                try
                {
                    foreach(string file in Directory.GetFiles(profilesFolder))
                    {
                        try
                        {
                            if(Path.GetExtension(file) == ".json")
                            {
                                Profile profile = Profile.Load(file);
                                if(profile != null) profiles.Add(profile);
                            }
                        }
                        catch(Exception e)
                        {
                            message += $"Failed to load profile '{Path.GetFileName(file)}': {e.Message}\n";
                        }
                        
                    }
                }
                catch(Exception e)
                {
                    message = "Error while loading profiles: " + e.Message;
                }

            }

            SortProfiles();

            Console.Clear();

            Console.ForegroundColor = ConsoleColor.Red;
            if(message != "") Console.WriteLine(message);
            Console.ForegroundColor = ConsoleColor.Gray;
            Console.WriteLine(profiles.Count == 0 ? "No profiles found" : "Loaded " + profiles.Count + " profiles.");
        }
        

        public void SaveProfiles()
        {
            try
            {
                for(int i = 0; i < profiles.Count; i++)
                {
                    profiles[i].Index = i;
                    profiles[i].Save(profilesFolder, defaultOptions);

                }

                //Deletes all the files that are not in the profiles list
                foreach(string file in Directory.GetFiles(profilesFolder))
                {
                    if(Path.GetExtension(file) == ".json")
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        if(!profiles.Any(p => p.Name == name))
                        {
                            File.Delete(file);
                        }
                    }
                }
                
                //JsonDataManager.Save(profiles, profilesFolder, defaultOptions);
            }
            catch(Exception e)
            {
                Console.WriteLine("Error while saving profiles: " + e.Message);
            }
        }
        
        public void SortProfiles()
        {
            if(profiles.Count > 0) profiles = profiles.OrderBy(p => p.Index).ToList();
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
            Console.WriteLine(help);

            Console.ForegroundColor = prevColor;
            Console.Write("\n(Enter to continue)");
            Console.ReadLine();
        }

        public void EnterIndexProfile(int index)
        {
            Profile profile = profiles.GetElement(index);
            LoadProfile(profile);
        }

        #region Mod Commands
        public void C_ToggleMod(string[] args)
        {

            if(args.Length == 0) throw new Exception("No index provided");

            //Modes: 0 = toggle; 1 = enable; 2 = disable
            int mode = 0;

            //If the first argument is either 'e' or 'd', it means a change in mode
            if(args[0] == "e") mode = 1;
            else if(args[0] == "d") mode = 2;

            //Means the mode was changed
            if(mode != 0)
            {
                if(args.Length == 1) throw new Exception("No index provided");
                args = new string[] { args[1] };
            }

            if(args.Length > 1) throw new Exception("Too many arguments");


            string[] indexes = args[0].Split(',', StringSplitOptions.TrimEntries);

            List<FileSystemInfo> toggledMods = new List<FileSystemInfo>();

            //Goes through all the indexes separated by commas, and adds the mods to be toggled.
            //If the user does something like: 1,2,6-8,10
            //It'll go through each index/range adding all the mods 
            if(indexes.Length > 1) Console.WriteLine("Calculating mods to toggle...");
            for(int i = 0; i < indexes.Length; i++) 
            {
                string currentIndex = indexes[i];
                if(currentIndex.Contains('-')) //Checks for a range
                {
                    string[] minMax = currentIndex.Split('-', StringSplitOptions.TrimEntries);
                    if(minMax.Length != 2) throw new Exception("Invalid range");

                    int num1 = int.Parse(minMax[0]);
                    int num2 = int.Parse(minMax[1]);

                    if(num1 >= allMods.Length || num2 >= allMods.Length) throw new Exception("Index out of range");

                    //Gets the range of mods to toggle
                    FileSystemInfo[] modsToToggle = allMods[Math.Min(num1, num2)..(Math.Max(num1, num2) + 1)];

                    Console.Clear();
                    for(int j = 0; j < modsToToggle.Length; j++)
                    {
                        toggledMods.Add(modsToToggle[j]);
                    }
                }
                else if(currentIndex == "all")
                {
                    if(currentFilter != "") toggledMods.AddRange(filteredMods);
                    else toggledMods.AddRange(allMods);
                }
                else
                {
                    FileSystemInfo mod = allMods.GetElement(currentIndex, m => m.Name);
                    toggledMods.Add(mod);
                }
            }

            Console.Clear();

            string action = "Toggl";
            if(mode == 1) action = "Enabl";
            else if(mode == 2) action = "Disabl";

            int toggled = 0;
            //Toggles all the mods specified
            for(int i = 0; i < toggledMods.Count; i++)
            {
                FileSystemInfo mod = toggledMods[i];
                Console.WriteLine($"{action}ing {mod.Name}...");

                bool isEnabled = IsEnabled(mod);
                if(mode == 1 && isEnabled) continue;
                else if(mode == 2 && !isEnabled) continue;

                //Decides wether to enable or disable the mod
                bool enable;
                switch(mode)
                {
                    case 0: //Toggle
                        enable = !isEnabled;
                        break;
                    case 1: //Enable
                        enable = true;
                        break;

                    case 2: //Disable
                        enable = false;
                        break;
                    default:
                        throw new Exception("INVALID MODE WTF DID YOU DO");
                }

                ToggleMod(mod, enable);
                toggled++;
            }
;
            lastCommandOutput = toggled == 1 ? $"&g{action}ed {toggledMods[0].Name}" : $"&g{action}ed {toggled} mods";
            
        }
        public void C_EnableMod(string[] args) 
        {
            string[] modifier = { "e" };

            //Adds 'e' as the first arg, because it means enable
            C_ToggleMod(modifier.Concat(args).ToArray());
        }
        public void C_DisableMod(string[] args) 
        {
            string[] modifier = { "d" };

            //Adds 'd' as the first arg, because it means disable
            C_ToggleMod(modifier.Concat(args).ToArray());
        }

        public void C_Open(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index or options provided");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            string arg = args[0];
            int index;
            string path;
            bool folder = false;
            if(arg.Contains("exe"))
            {
                path = currentProfile.ExecutablePath;
                if(path == "None")
                {
                    lastCommandOutput = "&mNo executable path set";
                    return;
                }
            }
            else if(arg.Contains("unused"))
            {
                path = currentProfile.UnusedModsPath;
                folder = true;
            }
            else if(arg.Contains("mod"))
            {
                path = currentProfile.ModsPath;
                folder = true;
            }
            else
            {
                FileSystemInfo mod = allMods.GetElement(arg, m => m.Name);
                path = mod.FullName;
            }

            if(!Directory.Exists(path) && !File.Exists(path)) throw new Exception("That directory or file no longer exists");

            string argument = folder ? path : "/select, \"" + path + "\"";
            Process.Start("explorer.exe", argument);

            if(path == currentProfile.ExecutablePath) lastCommandOutput = "&gOpened the executable path";
            else if(path == currentProfile.ModsPath) lastCommandOutput = "&gOpened the mods folder";
            else if(path == currentProfile.UnusedModsPath) lastCommandOutput = "&gOpened the unused mods folder";
            else lastCommandOutput = $"&gOpened '{Path.GetFileName(path)}' location";
        }
        public void C_DeleteMod(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index provided");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            FileSystemInfo mod = allMods.GetElement(args[0], m => m.Name);

            if(!YesNoAnswer($"Delete '{mod.Name}' forever? "))
            {
                lastCommandOutput = $"&rCancelled";
                return;
            }

            Console.WriteLine("Deleting mod...");
            DeleteFileSystemInfo(mod, true);

            lastCommandOutput = $"&gDeleted {mod.Name}";
        }
        public void C_RenameMod(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index provided");
            else if(args.Length > 2) throw new Exception("Too many arguments");

            FileSystemInfo mod = allMods.GetElement(args[0], m => m.Name);

            string newName = "";
            if(args.Length == 2) newName = args[1];
            else
            {
                Console.Write("New name: ");
                newName = Console.ReadLine().Trim();
            }
            if(string.IsNullOrEmpty(newName) || string.IsNullOrWhiteSpace(newName)) throw new Exception("Name cannot be empty");
            if(!IsDirectory(mod.FullName)) newName += Path.GetExtension(mod.FullName);
            if(allMods.Any(m => m.Name == newName)) throw new Exception("Name already exists");

            Console.WriteLine("Renaming mod...");
            RenameFileSystemInfo(mod, newName);
            lastCommandOutput = $"&gRenamed {mod.Name} to {newName}";
        }
        
        public void C_Sort(string[] args)
        {
            if(args.Length == 0) throw new Exception("No options provided");
            else if(args.Length > 2) throw new Exception("Too many arguments");

            Profile.SortType sortType;
            bool ascending = true;

            switch(args[0])
            {
                case "name":
                    sortType = Profile.SortType.Name;
                    break;
                case "date":
                    sortType = Profile.SortType.Date;
                    break;
                case "enabled":
                    sortType = Profile.SortType.Enabled;
                    break;
                default:
                    throw new Exception("Invalid sort type");
            }
            if(args.Length > 1 && args[1].Contains("desc")) ascending = false;

            currentProfile.SortBy = sortType;
            currentProfile.SortAscending = ascending;
            currentProfile.Save(profilesFolder, defaultOptions);
        }
        
        #endregion

        public bool IsEnabled(FileSystemInfo mod)
        {
            return Directory.GetParent(mod.FullName).FullName.Equals(currentProfile.ModsPath);
        }

        public void AutoToggleMod(int index)
        {
            FileSystemInfo mod = allMods.GetElement(index);
            bool enable = Directory.GetParent(mod.FullName).FullName.Equals(currentProfile.UnusedModsPath);
            ToggleMod(mod, enable);
        }

        public void ToggleMod(int index, bool enable, bool log = true)
        {
            FileSystemInfo mod = allMods.GetElement(index);
            ToggleMod(mod, enable, log);
        }

        public void ToggleMod(FileSystemInfo mod, bool enable, bool log = true)
        {
            if(!mod.Exists) throw new Exception("The mod no longer exists");

            //bool enabled = Directory.GetParent(mod.FullName).FullName.Equals(currentProfile.ModsPath);

            string destFolder = enable ? currentProfile.ModsPath : currentProfile.UnusedModsPath;
            string action = enable ? "Enabl" : "Disabl";

            if(destFolder == Directory.GetParent(mod.FullName).FullName)
            {
                lastCommandOutput = $"&r{mod.Name} already {action.ToLower()}ed";
                return;
            }

            MoveFileSystemInfo(mod, destFolder + "\\" + mod.Name);

            if(log) Console.WriteLine($"{action}ing mod...");

            lastCommandOutput = $"&g{action}ed {mod.Name}";
        }

        public void MoveFileSystemInfo(FileSystemInfo file, string destination)
        {
            if(IsDirectory(file.FullName))
            {
                Directory.Move(file.FullName, destination);
            }
            else
            {
                File.Move(file.FullName, destination);
            }

        }

        public void RenameFileSystemInfo(FileSystemInfo file, string newName)
        {
            MoveFileSystemInfo(file, Directory.GetParent(file.FullName) + "\\" + newName);
        }
        
        public void DeleteFileSystemInfo(FileSystemInfo file, bool recursive = false)
        {
            if(IsDirectory(file.FullName))
            {
                Directory.Delete(file.FullName, recursive);
            }
            else
            {
                File.Delete(file.FullName);
            }
        }

        public void LoadProfile(Profile profile)
        {
            currentProfile = profile;
            ExecuteCommands(startAction: LoadMods, errorAction: HandleCommandError, modCommands);
        }

        public void LoadMods()
        {
            currentMenu = MenuType.Mods;
            Console.Write("Loading mods...");
            ConsoleColor prevColor = Console.ForegroundColor;

            FileSystemInfo[] mods;
            FileSystemInfo[] unusedMods;

            int duplicateIndex;

            do
            {
                mods = new DirectoryInfo(currentProfile.ModsPath).GetFileSystemInfos();
                unusedMods = new DirectoryInfo(currentProfile.UnusedModsPath).GetFileSystemInfos();
                IEnumerable<FileSystemInfo> unsortedMods = mods.Concat(unusedMods);

                switch(currentProfile.SortBy)
                {
                    case Profile.SortType.Name:
                        if(currentProfile.SortAscending) unsortedMods = unsortedMods.OrderBy(m => m.Name);
                        else unsortedMods = unsortedMods.OrderByDescending(m => m.Name);
                        break;
                    case Profile.SortType.Date:
                        if(currentProfile.SortAscending) unsortedMods = unsortedMods.OrderBy(m => m.CreationTime);
                        else unsortedMods = unsortedMods.OrderByDescending(m => m.CreationTime);
                        break;
                    case Profile.SortType.Enabled:
                        if(currentProfile.SortAscending) unsortedMods = unsortedMods.OrderBy(m => IsEnabled(m));
                        else unsortedMods = unsortedMods.OrderByDescending(m => IsEnabled(m));
                        break;
                    default:
                        break;
                }
                allMods = unsortedMods.ToArray();

                //Checks for duplicates
                duplicateIndex = FindFirstDuplicate(mods, unusedMods);
                if(duplicateIndex != -1)
                {
                    Console.Clear();
                    Console.ForegroundColor = ConsoleColor.Red;

                    FileSystemInfo duplicate = unusedMods[duplicateIndex];
                    Console.WriteLine($"Duplicate mods found. Name: {duplicate.Name}");

                    Console.ForegroundColor = ConsoleColor.Gray;
                    bool rename = YesNoAnswer("\nRename unused mod? ");

                    Console.ForegroundColor = prevColor;
                    Console.WriteLine();
                    if(rename)
                    {
                        C_RenameMod(new string[] { duplicateIndex.ToString() });
                    }
                    else
                    {
                        bool delete = YesNoAnswer("Delete unused mod forever? ");
                        if(delete) DeleteFileSystemInfo(duplicate, true);
                        else throw new Exception("Cannot have duplicate mods");
                    }
                    break;
                }

            }
            while(duplicateIndex != -1);


            Console.Clear();
            DisplayDetails(currentProfile);
            DisplayMods(mods);

        }

        public void DisplayMods(FileSystemInfo[] usedMods)
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.WriteLine();

            //Shows the current filter
            Console.ForegroundColor = ConsoleColor.Magenta;
            if(currentFilter != "") Console.WriteLine($"Filter: {currentFilter}");
            Console.ForegroundColor = prevColor;

            filteredMods.Clear();
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Mods:");
            for(int i = 0; i < allMods.Length; i++)
            {
                FileSystemInfo mod = allMods[i];
                bool used = usedMods.Contains(mod);
                string check = used ? " - Enabled" : " - Disabled";

                //The string that will be checked against the filter
                string filterString = $"{mod.Name}{check}";
                if(currentFilter != "" && !PassesFilter(filterString, currentFilter)) continue;

                if(currentFilter != "") filteredMods.Add(mod);
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{i}.- {mod.Name}");
                Console.ForegroundColor = used ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine(check);
                Console.ForegroundColor = prevColor;
            }
            Console.ForegroundColor = prevColor;
            Console.WriteLine();
        }
    }
}
