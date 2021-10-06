using Newtonsoft.Json;
using Newtonsoft.Json.Schema.Generation;
using System;
using System.Collections.Generic;

namespace TFSBlasterLib.Service
{
    internal class JsonService
    {
        public FileService FileService;
        public LogService LogService;

        public virtual T ReadJsonFile<T>(Uri relativeFilePath)
        {
            var jsonText = FileService.ReadRelativeFileText(relativeFilePath);

            Log("Parsing JSON read from file");

            var result = default(T);

            try
            {
                result = JsonConvert.DeserializeObject<T>(jsonText, new JsonSerializerSettings()
                {
                    NullValueHandling = NullValueHandling.Ignore
                });
            }
            catch (System.Exception ex)
            {
                Log(ex.ToString());
                throw;
            }

            return result;
        }

        public virtual string GetSchema<T>()
        {
            var generator = new JSchemaGenerator();
            var schema = generator.Generate(typeof(T));
            return schema.ToString();
        }

        public virtual string Serialize(object toSerialize)
        {
            return JsonConvert.SerializeObject(toSerialize, new JsonSerializerSettings()
            {
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.Indented
            });
        }

        public virtual Dictionary<string,object> DeserializeAnonymous(string jsonText)
        {
            return JsonConvert.DeserializeObject<Dictionary<string,object>>(jsonText, new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented
            });
        }

        private void Log(string message)
        {
            LogService.LogToFile(nameof(JsonService), message);
        }
    }
}
