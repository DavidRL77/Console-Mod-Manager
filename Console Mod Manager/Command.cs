using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Console_Mod_Manager
{
    public class Command
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Usage { get; set; }
        public string[] Aliases { get; set; }
        public Action<string[]> Function { get; set; }

        public Command(string name, string description, string usage, Action<string[]> function, params string[] aliases)
        {
            //Name cannot be a number
            if(int.TryParse(name, out _)) throw new ArgumentException("Name cannot be a number");

            Name = name;
            Description = description;
            Usage = usage;
            Function = function;
            Aliases = aliases;
        }

        public string Help()
        {
            return $"{Name} - {Description}\n\nUsage: \n{Usage}";
        }

        public bool IsCommand(string command)
        {
            return Name.ToLower() == command.ToLower() || Aliases.Any(x => x.ToLower() == command.ToLower());
        }

        public void Execute(string[] args)
        {
            Function(args);
        }
    }

    public class CommandParser
    {
        public List<Command> commands { get; private set; }

        /// <summary>
        /// What will be called when help for a command is requested
        /// </summary>
        public Action<string> HelpAction { get; set; }

        /// <summary>
        /// What will be called when the command is an index
        /// </summary>
        /// <param name="helpAction"></param>
        public Action<int> IndexAction { get; set; }

        public CommandParser(Action<string> helpAction)
        {
            commands = new List<Command>();
            HelpAction = helpAction;
        }

        public CommandParser(Action<string> helpAction, params Command[] commands)
        {
            this.commands = commands.ToList();
            HelpAction = helpAction;
        }
        
        /// <summary>
        /// Creates a new CommandParser
        /// </summary>
        /// <param name="helpAction"></param>
        /// <param name="indexAction">The action that will be called when the command is a number</param>
        /// <param name="commands"></param>
        public CommandParser(Action<string> helpAction, Action<int> indexAction, params Command[] commands)
        {
            this.commands = commands.ToList();
            IndexAction = indexAction;
            HelpAction = helpAction;
        }

        public void AddCommand(Command command)
        {
            commands.Add(command);
        }
        public void AddCommand(string name, string description, string usage, Action<string[]> action, params string[] aliases)
        {
            commands.Add(new Command(name, description, usage, action, aliases));
        }
        public void AddCommands(params Command[] commandRange)
        {
            commands.AddRange(commandRange);
        }
        public void SetCommands(params Command[] commandRange)
        {
            commands = commandRange.ToList();
        }

        public void TryExecute(string command)
        {
            command = command.Trim();

            //Separates the command from the arguments
            string[] commandSplit = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string commandName = commandSplit[0];

            //If the command is a number, then execute the index action
            if(int.TryParse(commandName, out int index))
            {
                if(IndexAction != null) IndexAction(index);
                return;
            }

            string[] args = commandSplit.Skip(1).ToArray();

            //Finds the command
            Command cmd = commands.Find(x => x.IsCommand(commandName));
            if(cmd == null) throw new Exception($"Invalid command '{commandName}'");

            //Cleans up the arguments, removing quotation marks and slashes
            for(int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                arg = arg.Trim().Replace("\"", "");
                args[i] = arg;
            }

            if(args.Length == 1 && args[0] == "help" || args.Length == 1 && args[0] == "?") HelpAction(cmd.Help());
            else cmd.Execute(args);
        }
        
        public string ParseToString(char separator)
        {
            return string.Join(separator, commands.Select(x => x.Name));
        }

        public override string ToString()
        {
            return string.Join(' ', commands.Select(x => x.Name));
        }
    }
}
