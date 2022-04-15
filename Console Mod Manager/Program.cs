using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

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
        public CommandParser modCommands;

        private Profile currentProfile;

        public string lastCommandOutput = "";
        public FileSystemInfo[] allMods;

        private string currentFilter = "";

        static void Main(string[] args)
        {
            Program p = new Program();
            p.Init();
            p.Run();
        }

        public void Init()
        {
            Command filterCommand = new Command("filter", "Filters the items that are displayed. Empty to clear filter.", "filter <filter>", C_Filter, "f", "search");

            profileCommands = new CommandParser(helpAction: DisplayHelp, indexAction: EnterIndexProfile,
            new Command("create", "Creates a new profile", "create <name> <mods_folder> <unused_mods_folder>", C_CreateProfile, "cr", "c"),
            new Command("delete", "Deletes a profile", "delete <index>", C_DeleteProfile, "de", "d", "del", "remove"),
            new Command("edit", "Edits the path of a folder in a profile", "change mod/unused/exe <new_path>", C_EditProfile, "change", "ch", "ed"),
            new Command("details", "Shows all the details of a profile", "details <index>", C_DetailsProfile, "see", "type", "detail"),
            new Command("load", "Loads a profile", "load <index>", C_EnterProfile, "enter", "en"),
            new Command("rename", "Renames a profile", "rename <index> <new_name>", C_RenameProfile, "re", "r"),
            filterCommand
            );

            modCommands = new CommandParser(helpAction: DisplayHelp, indexAction: ToggleMod,
                new Command("toggle", "Toggles a mod", "toggle <index>", C_ToggleMod, "togle", "t"),
                new Command("delete", "Deletes a mod forever", "delete <index>", C_DeleteMod, "del", "remove", "de", "d"),
                new Command("rename", "Renames a mod", "rename <index> <new_name>", C_RenameMod, "re", "r"),
                new Command("open", "Opens the directory of a mod or profile folder", "open mod/unused/exe/<index>", C_Open, "op", "go"),
                filterCommand
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
                if(startAction != null) startAction();

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
                    lastCommandOutput = "&rError: " + e.Message;
                }

                Console.Clear();

            } while(answer != "exit");
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
                    break;
            }
        }

        public void C_DeleteProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile = GetProfile(args[0]);
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
            SaveProfiles();
        }

        public void C_DetailsProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile = GetProfile(args[0]);

            DisplayDetails(profile);

            Console.Write("\n\n(Enter to continue)");
            Console.ReadLine();
        }

        public void C_EnterProfile(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index specified");
            if(args.Length > 1) throw new Exception("Too many arguments");

            Profile profile = GetProfile(args[0]);
            LoadProfile(profile);
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
            SaveProfiles();
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

            Console.ForegroundColor = prevColor;
        }

        public void DisplayProfiles()
        {
            ConsoleColor prevColor = Console.ForegroundColor;


            //Shows the current filter
            Console.ForegroundColor = ConsoleColor.Magenta;
            if(currentFilter != "") Console.WriteLine($"Filter: {currentFilter}");
            Console.ForegroundColor = prevColor;

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
            Console.WriteLine(help);

            Console.ForegroundColor = prevColor;
            Console.Write("\n(Enter to continue)");
            Console.ReadLine();
        }

        public void EnterIndexProfile(int index)
        {
            Profile profile = GetProfile(index);
            LoadProfile(profile);
        }

        #region Mod Commands
        public void C_ToggleMod(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index provided");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            if(args[0] == "all") args[0] = "0-" + (allMods.Length - 1); //Replaces the keyword 'all' for a full range

            if(args[0].Contains('-')) //Checks for a range
            {
                string[] minMax = args[0].Split('-', StringSplitOptions.TrimEntries);
                if(minMax.Length != 2) throw new Exception("Invalid range");

                Console.WriteLine("Calculating mods to toggle...");
                int num1 = int.Parse(minMax[0]);
                int num2 = int.Parse(minMax[1]);

                if(num1 >= allMods.Length || num2 >= allMods.Length) throw new Exception("Index out of range");

                //Gets the range of mods to toggle
                FileSystemInfo[] modsToToggle = allMods[Math.Min(num1, num2)..(Math.Max(num1, num2) + 1)];

                Console.Clear();
                for(int i = 0; i < modsToToggle.Length; i++)
                {
                    Console.Write("\rToggling" + modsToToggle[i].Name + "...");
                    ToggleMod(modsToToggle[i], false);
                }
                lastCommandOutput = "&gToggled " + modsToToggle.Length + " mods";
            }
            else
            {
                FileSystemInfo mod = GetMod(args[0]);
                ToggleMod(mod);
            }
        }
        public void C_Open(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index provided");
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
                FileSystemInfo mod = GetMod(arg);
                path = mod.FullName;
            }

            if(!Directory.Exists(path) && !File.Exists(path)) throw new Exception("That directory or file no longer exists");

            string argument = folder ? path : "/select, \"" + path + "\"";
            Process.Start("explorer.exe", argument);

            if(path == currentProfile.ExecutablePath) lastCommandOutput = "&gOpened the executable path";
            else if(path == currentProfile.ModsPath) lastCommandOutput = "&gOpened the mods folder";
            else if(path == currentProfile.UnusedModsPath) lastCommandOutput = "&gOpened the unused mods folder";
            else lastCommandOutput =  $"&gOpened '{Path.GetFileName(path)}' location";
        }
        public void C_DeleteMod(string[] args)
        {
            if(args.Length == 0) throw new Exception("No index provided");
            else if(args.Length > 1) throw new Exception("Too many arguments");

            FileSystemInfo mod = GetMod(args[0]);

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

            FileSystemInfo mod = GetMod(args[0]);

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
        #endregion

        public void ToggleMod(int index)
        {
            FileSystemInfo mod = GetMod(index);
            ToggleMod(mod);
        }

        public void ToggleMod(FileSystemInfo mod, bool log = true)
        {
            if(!mod.Exists) throw new Exception("The mod no longer exists");

            bool enabled = Directory.GetParent(mod.FullName).FullName.Equals(currentProfile.ModsPath);
            string destFolder = enabled ? currentProfile.UnusedModsPath : currentProfile.ModsPath;

            MoveFileSystemInfo(mod, destFolder + "\\" + mod.Name);

            if(log) Console.WriteLine($"Toggling mod...");

            lastCommandOutput = $"&gToggled {mod.Name}";
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

        public FileSystemInfo GetMod(string index)
        {
            int i;
            if(int.TryParse(index, out i))
            {
                return GetMod(i);
            }
            else
            {
                throw new Exception("Please enter a number");
            }
        }
        public FileSystemInfo GetMod(int index)
        {
            if(index >= allMods.Length) throw new Exception("Index out of range");
            else if(index < 0) throw new Exception("Index must be positive");
            return allMods[index];
        }

        public void LoadProfile(Profile profile)
        {
            currentProfile = profile;
            ExecuteCommands(LoadMods, modCommands);
        }

        public void LoadMods()
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            FileSystemInfo[] mods;
            FileSystemInfo[] unusedMods;

            int duplicateIndex;

            do
            {
                mods = new DirectoryInfo(currentProfile.ModsPath).GetFileSystemInfos();
                unusedMods = new DirectoryInfo(currentProfile.UnusedModsPath).GetFileSystemInfos();
                allMods = mods.Concat(unusedMods).ToArray();

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

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Mods: ");
            for(int i = 0; i < allMods.Length; i++)
            {
                FileSystemInfo mod = allMods[i];
                bool used = usedMods.Contains(mod);
                string check = used ? " - Enabled" : " - Disabled";

                //The string that will be checked against the filter
                string filterString = $"{mod.Name}{check}";
                if(!PassesFilter(filterString, currentFilter)) continue;

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"{i}.- {mod.Name}");
                Console.ForegroundColor = used ? ConsoleColor.Green : ConsoleColor.Red;
                Console.WriteLine(check);
                Console.ForegroundColor = prevColor;
            }
            Console.ForegroundColor = prevColor;
            Console.WriteLine();
        }

        /// <summary>
        /// Will return the index of the first duplicate found, otherwise -1
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public int FindFirstDuplicate(FileSystemInfo[] first, FileSystemInfo[] second)
        {
            for(int i = 0; i < first.Length; i++)
            {
                string current = first[i].Name;
                for(int j = 0; j < second.Length; j++)
                {
                    FileSystemInfo secondCurrent = second[j];
                    if(secondCurrent.Name == current)
                    {
                        return j;
                    }
                }
            }
            return -1;
        }

        public bool PassesFilter(string stringToCheck, string filter)
        {
            if(filter == "") return true;
            return stringToCheck.ToLower().Contains(filter);
        }

        public bool IsDirectory(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
        }
        public bool YesNoAnswer(string question, ConsoleColor color = ConsoleColor.Cyan)
        {
            ConsoleColor prevColor = Console.ForegroundColor;

            Console.ForegroundColor = color;
            Console.WriteLine(question + "(y/n)");
            Console.ForegroundColor = prevColor;

            ConsoleKeyInfo key;
            do
            {
                key = Console.ReadKey(true);
                if(key.KeyChar == 'y') { Console.WriteLine("y"); return true; }
                if(key.KeyChar == 'n') { Console.WriteLine("n"); return false; }
            } while(key.Key != ConsoleKey.Escape);


            Console.WriteLine("Esc");
            throw new Exception("Aborted");
        }
    }
}
