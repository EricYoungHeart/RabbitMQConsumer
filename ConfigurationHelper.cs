using Microsoft.Extensions.Configuration;
using System;

namespace Consumer
{
    public static class ConfigurationHelper
    {
        // Static field to hold the built configuration
        private static IConfiguration _configuration;

        // Static constructor runs once per application start
        static ConfigurationHelper()
        {
            _configuration = new ConfigurationBuilder()
                // .SetBasePath(Directory.GetCurrentDirectory()) - так нельзя, это ловушка! при переходе на Windows Service это будет c:\Windows\System32!
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .Build();
        }
        public static T GetConfig<T>(string sectionName) where T : new()
        {
            var section = _configuration.GetSection(sectionName);

            if (!section.Exists())
                throw new InvalidOperationException($"Configuration section '{sectionName}' is missing! " +
                    $"Please check appsettings.json.");

            var config = section.Get<T>();
            if (config == null)
                throw new InvalidOperationException($"Section '{sectionName}' exists but could not be mapped to type {typeof(T).Name}.");

            return config;
        }

    }

}