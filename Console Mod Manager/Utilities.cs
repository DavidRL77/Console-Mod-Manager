using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Console_Mod_Manager
{
    static class Utilities
    {
        /// <summary>
        /// Will return the index of the first duplicate found, otherwise -1
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static int FindFirstDuplicate(FileSystemInfo[] first, FileSystemInfo[] second)
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

        public static bool PassesFilter(string stringToCheck, string filter)
        {
            if(filter == "") return true;

            stringToCheck = stringToCheck.ToLower();
            filter = filter.ToLower();

            string[] filterSplit = filter.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if(!filterSplit.Contains("||")) //No logical operators used
            {
                //The fast check, returns as soon as a word evaluates false
                for(int i = 0; i < filterSplit.Length; i++)
                {
                    string current = filterSplit[i];

                    if(current.StartsWith('-') && current.Length > 1) //If the filter starts with a '-' and it has something after it
                    {
                        if(stringToCheck.Contains(current[1..])) return false;
                    }
                    else
                    {
                        if(!stringToCheck.Contains(current)) return false;
                    }

                }
                return true;
            }
            else
            {
                bool orOperator = false;
                bool passed = false;

                //The bool operator check
                for(int i = 0; i < filterSplit.Length; i++)
                {
                    string current = filterSplit[i];

                    if(current == "||")
                    {
                        orOperator = true;
                        continue;
                    }

                    //If the first result in the 'or' operation was true, no need to check the rest
                    if(orOperator && passed) { orOperator = false; continue; }
                    if(current.StartsWith('-') && current.Length > 1) //If the filter starts with a '-' and it has something after it
                    {
                        if(stringToCheck.Contains(current[1..])) passed = false;
                        else passed = true;
                    }
                    else
                    {
                        if(!stringToCheck.Contains(current)) passed = false;
                        else passed = true;
                    }

                    //Returns false when:
                    //It is the last word in an 'or' operation and it didn't pass
                    //It isn't an 'or' operation and it didn't pass
                    //Always check at least the first two words before returning false
                    if(!passed && i > 0) return false;

                    orOperator = false;
                }
                return true;
            }
        }

        /// <summary>
        /// Gets the only element that matches the predicate, otherwise null
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="getProp"></param>
        /// <returns></returns>
        public static T GetUniqueElement<T>(IEnumerable<T> collection, Func<T, bool> predicate)
        {
            T element = default;
            bool found = false;
            foreach(T item in collection)
            {
                if(predicate(item))
                {
                    if(found) return default;
                    element = item;
                    found = true;
                }
            }
            return element;
        }

        public static bool IsDirectory(string path)
        {
            FileAttributes attr = File.GetAttributes(path);
            return attr.HasFlag(FileAttributes.Directory);
        }
        
        public static bool YesNoAnswer(string question, ConsoleColor color = ConsoleColor.Cyan)
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

        public static int IndexOf<T>(this IEnumerable<T> collection, T element)
        {
            int i = 0;
            EqualityComparer<T> comparer = EqualityComparer<T>.Default;
            foreach(T item in collection)
            {
                if(comparer.Equals(item, element)) return i;
                i++;
            }
            return -1;
        }

        /// <summary>
        /// Gets an element from a collection at the index specified
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static T GetElement<T>(this IEnumerable<T> collection, int index)
        {
            if(index >= collection.Count()) throw new Exception("Index out of range");
            else if(index < 0) throw new Exception("Index must be positive");
            return collection.ElementAt(index);
        }

        public static T GetElement<T>(this IEnumerable<T> collection, string name, Func<T, string> propertyToCompare)
        {
            int index = collection.GetIndexFromString(name, propertyToCompare);
            return collection.ElementAt(index);
        }

        /// <summary>
        /// If the name is a number, it will return the number, otherwise it will look for elements that match the name
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="collection"></param>
        /// <param name="name"></param>
        /// <param name="property"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static int GetIndexFromString<T>(this IEnumerable<T> collection, string name, Func<T, string> propertyToCompare)
        {
            if(int.TryParse(name, out int i))
            {
                return i;
            }
            else
            {
                T element = GetUniqueElement(collection, m => propertyToCompare(m).ToLower().StartsWith(name.ToLower()));
                if(element != null) return collection.IndexOf(element);

                throw new Exception($"Could not find element with name '{name}'");
            }
        }
    }
}
