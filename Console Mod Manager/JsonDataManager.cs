using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;
using System.IO;

namespace Console_Mod_Manager
{
    public static class JsonDataManager
    {
        /// <summary>
        /// Returns a json string of the given object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectToSerialize"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static string Serialize(object objectToSerialize, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Serialize(objectToSerialize, options);
        }

        /// <summary>
        /// Turns a given json string into an object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="json"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static T Deserialize<T>(string json, JsonSerializerOptions options = null)
        {
            return JsonSerializer.Deserialize<T>(json, options);
        }

        /// <summary>
        /// Converts a given object to a json string and saves it to a file
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="objectToSerialize"></param>
        /// <param name="path"></param>
        /// <param name="options"></param>
        public static void Save(object objectToSerialize, string path, JsonSerializerOptions options = null)
        {
            string json = Serialize(objectToSerialize, options);
            string directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, json);
        }

        /// <summary>
        /// Reads the Json of a file and returns a converted object
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="path"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static T Load<T>(string path, JsonSerializerOptions options = null)
        {
            string json = File.ReadAllText(path);
            if(json.Length == 0) return default;
            return Deserialize<T>(json, options);
        }
    }
}
