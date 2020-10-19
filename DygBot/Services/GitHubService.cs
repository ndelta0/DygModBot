using Discord;

using DygBot.Models;

using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using static DygBot.Modules.ModerationModule.ReactionRoleClass;

namespace DygBot.Services
{
    public class GitHubService
    {
        private readonly HttpClient _client;
        private readonly string _gitHubApiToken = Environment.GetEnvironmentVariable("GITHUB_API_TOKEN");
        private readonly Uri _configUri;
        private string _sha;
        private readonly JsonSerializerSettings _settings;

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

            _settings = new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            };
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
            Config = JsonConvert.DeserializeObject<ConfigClass>(decoded, _settings);
            _sha = responseObject.Sha; // Update SHA (for updating config later)
        }

        public async Task UploadConfig()
        {
            HttpRequestMessage requestMessage = new HttpRequestMessage(HttpMethod.Put, _configUri); // Create a request message
            requestMessage.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("token", _gitHubApiToken ?? throw new ArgumentNullException("GitHub token not provided")); // Add authorization token
            requestMessage.Headers.UserAgent.ParseAdd("DygModBot by D3LT4PL/1.0"); // Add UserAgent header

            var requestObject = new GitHubPutRequest { Sha = _sha };    // Copy the old config SHA

            var json = JsonConvert.SerializeObject(Config, Formatting.Indented, _settings);

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
                public class ReactionRole
                {
                    public ReactionAction Action { get; set; }
                    public Dictionary<string, ulong> Roles { get; set; } = new Dictionary<string, ulong>();
                }
                public string Prefix { get; set; } = "db!";
                public List<ulong> ManagementRoles { get; set; } = new List<ulong>();
                public Dictionary<ulong, List<string>> AutoReact { get; set; } = new Dictionary<ulong, List<string>>();
                public Dictionary<ulong, CountChannelClass> CountChannels { get; set; } = new Dictionary<ulong, CountChannelClass>();
                public Dictionary<ulong, ulong> VcTextRole { get; set; } = new Dictionary<ulong, ulong>();
                public Dictionary<string, List<ulong>> CommandLimit { get; set; } = new Dictionary<string, List<ulong>>();
                public ulong NotificationChannelId { get; set; } = default;
                public ulong LogChannel { get; set; } = default;
                public Dictionary<ulong, Dictionary<ulong, List<ReactionRole>>> ReactionRoles { get; set; } = new Dictionary<ulong, Dictionary<ulong, List<ReactionRole>>>();
                public Dictionary<string, HashSet<ulong>> AllowedReactions { get; set; } = new Dictionary<string, HashSet<ulong>>();
                public Dictionary<ulong, Gender> HalfAnHourConfig { get; set; } = new Dictionary<ulong, Gender>();
                public Dictionary<Gender, ulong> OcChannels { get; set; } = new Dictionary<Gender, ulong>();
                public Color ServerColor { get; set; } = RandomColor();
                public Dictionary<string, string> AdditionalConfig { get; set; } = new Dictionary<string, string>();

            }
            public string DiscordToken { get; set; }
            public string RedditAppId { get; set; }
            public string RedditAppSecret { get; set; }
            public string RedditAppRefreshToken { get; set; }
            public Dictionary<ulong, ServerConfigClass> Servers { get; set; }
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

        private static Color RandomColor()
        {
            var rand = new Random();
            return new Color((uint)rand.Next(0xFFFFFF + 1));
        }
    }
}
