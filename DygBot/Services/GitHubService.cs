using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace DygBot.Services
{
    public class GitHubService
    {
        private readonly HttpClient _client;
        private readonly string _gitHubApiToken = Environment.GetEnvironmentVariable("GITHUB_API_TOKEN");
        private readonly Uri _configUri;
        private string _sha;

        public ConfigClass Config;

        public GitHubService(
            HttpClient client)
        {
            _client = client;
            var environment = Environment.GetEnvironmentVariable("ENVIRONMENT");
            if (environment == "DEVELOPMENT")
                _configUri = new Uri("https://api.github.com/repos/D3LT4PL/bot_config_repo/contents/dygmodbot_config.dev.json");
            else
                _configUri = new Uri("https://api.github.com/repos/D3LT4PL/bot_config_repo/contents/dygmodbot_config.json");

        }

        public async Task DownloadConfig()
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Get, _configUri); // Create a request message
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", _gitHubApiToken ?? throw new ArgumentNullException("GitHub token not provided")); // Add authorization token
            requestMessage.Headers.UserAgent.ParseAdd("DygModBot by D3LT4PL/1.0"); // Add UserAgent header

            var response = await _client.SendAsync(requestMessage); // Execute the request
            response.EnsureSuccessStatusCode(); // Throw exception if request wasn't successfull

            var content = await response.Content.ReadAsStringAsync();   // Read the request body

            var responseObject = JsonConvert.DeserializeObject<GitHubGetResponse>(content, new JsonSerializerSettings   // Deserialize response into object
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }   // Use snake_case naming strategy (html_url -> HtmlUrl)
            });

            Config = new ConfigClass();
            string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(responseObject.Content)); // Decode Base64 encoded string
            Config = JsonConvert.DeserializeObject<ConfigClass>(decoded, new JsonSerializerSettings  // Deserialize config into object
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver() // Use CamelCase naming strategy (boundChannels -> BoundChannels)
            });
            _sha = responseObject.Sha; // Update SHA (for updating config later)
        }

        public async Task UploadConfig()
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Put, _configUri); // Create a request message
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", _gitHubApiToken ?? throw new ArgumentNullException("GitHub token not provided")); // Add authorization token
            requestMessage.Headers.UserAgent.ParseAdd("DygModBot by D3LT4PL/1.0"); // Add UserAgent header

            var requestObject = new GitHubPutRequest { Sha = _sha };    // Copy the old config SHA

            var json = JsonConvert.SerializeObject(Config, Formatting.Indented, new JsonSerializerSettings   // Serialize config into json
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()     // Use CamelCase name resolver
            });

            requestObject.Content = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));   // Base64 encode config

            var requestJson = JsonConvert.SerializeObject(requestObject, new JsonSerializerSettings // Serialize request into json
            {
                ContractResolver = new DefaultContractResolver { NamingStrategy = new SnakeCaseNamingStrategy() }   // Use snake_case name resolver
            });

            var data = new StringContent(requestJson, Encoding.UTF8, "application/json");   // Create a string content object

            requestMessage.Content = data;  // Set request message content

            var response = await _client.SendAsync(requestMessage); // Execute the request
            response.EnsureSuccessStatusCode(); // Throw exception if not successfull

            await DownloadConfig();
        }

        public class ConfigClass
        {
            public class ServerConfigClass
            {
                public class CountChannelClass
                {
                    public enum CountPropertyEnum
                    {
                        Members,
                        Bans
                    }
                    public CountPropertyEnum Property { get; set; }
                    public string Template { get; set; }
                }
                public string Prefix { get; set; }
                public List<string> ManagementRoles { get; set; } = new List<string>();
                public Dictionary<string, List<string>> AutoReact { get; set; } = new Dictionary<string, List<string>>();
                public Dictionary<string, CountChannelClass> CountChannels { get; set; } = new Dictionary<string, CountChannelClass>();
                public Dictionary<string, string> VcTextRole { get; set; } = new Dictionary<string, string>();
            }
            public string DiscordToken { get; set; }
            public Dictionary<string, ServerConfigClass> Servers { get; set; }
        }

        public class GitHubGetResponse
        {
            public string Sha { get; set; }
            public string Content { get; set; }
        }

        public class GitHubPutRequest
        {
            public string Message { get; set; } = "Config Update (Bot)";
            public string Content { get; set; }
            public string Sha { get; set; }
        }
    }
}
