﻿using DiscordStats.DAL.Abstract;
using DiscordStats.Models;
using System.Net;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;

namespace DiscordStats.DAL.Concrete
{
    // It would be nice to use constructor injection here to inject the ability to get a web request
    // but that won't work in cases like this where we'd have to know the User and their bearer token
    // before creating the service to inject.  Instead we use method parameter injection.

    public class DiscordService : IDiscordService
    {
        // Use constructor injection to get the http client factory, which we'll use to get an http client
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IServerRepository _serverRepository;

        private DiscordDataDbContext _db = new DiscordDataDbContext();

        public DiscordService(IHttpClientFactory httpClientFactory, IServerRepository serverRepository)
        {
            _serverRepository = serverRepository;   
            _httpClientFactory = httpClientFactory;
        }


        // This method wraps up the minimum functionality needed to make a request
        // to an external dependency.  It cannot be unit tested, but we do need to fake it in
        // order to test code that uses it, i.e. GetCurrentUserGuilds below
        public async Task<string> GetJsonStringFromEndpoint(string bearerToken, string uri)
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Headers =
                    {
                        { HeaderNames.Accept, "application/json" },
                        { HeaderNames.Authorization, "Bearer " + bearerToken},
                        { HeaderNames.UserAgent, "DiscordStat" }
                    }
            };
            HttpClient httpClient = _httpClientFactory.CreateClient();
            // Note this is the blocking version.  Would be better to use the Async version
            HttpResponseMessage response = await httpClient.SendAsync(httpRequestMessage);
            // This is only a minimal version; make sure to cover all your bases here
            if (response.IsSuccessStatusCode)
            {
                // same here, this is blocking; use ReadAsStreamAsync instead
                string responseText = await response.Content.ReadAsStringAsync();
                return responseText;
            }
            else
            {
                // What to do if failure? Should throw specific exceptions that explain what happened
                throw new HttpRequestException();
            }
        }
        public async Task<string> GetJsonStringFromEndpointWithUserParam(string botToken, string uri)
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Headers =
                    {
                        { HeaderNames.Accept, "application/json" },
                        { HeaderNames.Authorization, "Bot " + botToken},
                        { HeaderNames.UserAgent, "DiscordStat" }
                    }
            };
            HttpClient httpClient = _httpClientFactory.CreateClient();
            // Note this is the blocking version.  Would be better to use the Async version
            HttpResponseMessage response = await httpClient.SendAsync(httpRequestMessage);
            // This is only a minimal version; make sure to cover all your bases here
            if (response.IsSuccessStatusCode)
            {
                // same here, this is blocking; use ReadAsStreamAsync instead
                string responseText = await response.Content.ReadAsStringAsync();
                return responseText;
            }
            else
            {
                // What to do if failure? Should throw specific exceptions that explain what happened
                throw new HttpRequestException();
            }
        }

        public async Task<string> GetJsonStringFromEndpointForBot(string botToken, string uri)
        {
            HttpRequestMessage httpRequestMessage = new HttpRequestMessage(HttpMethod.Get, uri)
            {
                Headers =
                {
                    { HeaderNames.Accept, "application/json" },
                    { HeaderNames.Authorization, "Bot " + botToken },
                    { HeaderNames.UserAgent, "DiscordStat" }

                }
            };
            HttpClient httpClient = _httpClientFactory.CreateClient();
            // Note this is the blocking version.  Would be better to use the Async version
            HttpResponseMessage response = await httpClient.SendAsync(httpRequestMessage);
            // This is only a minimal version; make sure to cover all your bases here
            if (response.IsSuccessStatusCode)
            {
                // same here, this is blocking; use ReadAsStreamAsync instead
                string responseText = await response.Content.ReadAsStringAsync();
                return "true";
            }
            else
            {
                // What to do if failure? Should throw specific exceptions that explain what happened
                return "false";
                throw new HttpRequestException();
            }
        }

        public async Task<List<Server>?> GetCurrentUserGuilds(string bearerToken, string botToken)
        {

            // Remember to handle errors here
            string response = await GetJsonStringFromEndpoint(bearerToken, $"https://discord.com/api/users/@me/guilds");
            // And here
            List<Server>? servers = JsonConvert.DeserializeObject<List<Server>>(response);
            foreach (Server server in servers)
            {
                server.HasBot = await CheckForBot(botToken, server.Id);
                if (server.HasBot == "true")
                {
                    var dbServers = _serverRepository.GetAll();
                    if (dbServers.Count() == 0)
                    {
                        Server? serverHasBot = await GetCurrentGuild(botToken, server.Id);

                        var servMemberCount = serverHasBot.Approximate_Member_Count;
                        _serverRepository.AddOrUpdate(new Server() { Id = server.Id, Name = server.Name, Owner = server.Owner, Icon = server.Icon, HasBot = server.HasBot, Approximate_Member_Count = servMemberCount });

                    }
                    else
                    {
                        foreach (var dbServer in dbServers)
                        {
                            if (server.Id != dbServer.Id)
                            {
                                Server? serverHasBot = await GetCurrentGuild(botToken, server.Id);

                                var servMemberCount = serverHasBot.Approximate_Member_Count;
                                _serverRepository.AddOrUpdate(new Server() { Id = server.Id, Name = server.Name, Owner = server.Owner, Icon = server.Icon, HasBot = server.HasBot, Approximate_Member_Count = servMemberCount });
                            }
                        }
                    }    
                }
            }
            return servers;
        }

        public async Task<User?> GetCurrentUserInfo(string bearerToken)
        {
            // Remember to handle errors here
            string response = await GetJsonStringFromEndpoint(bearerToken, "https://discord.com/api/users/@me");
            // And here
            User? userInfo = JsonConvert.DeserializeObject<User>(response);
            return userInfo;
        }

        public async Task<Server?> GetCurrentGuild(string botToken, string serverId)
        {
            string uri = "https://discord.com/api/guilds/" + serverId + "/preview";
            // Remember to handle errors here
            string response = await GetJsonStringFromEndpointWithUserParam(botToken, uri);
            // And here
            Server? server = JsonConvert.DeserializeObject<Server>(response);
            return server;
        }

        public async Task<string?> CheckForBot(string botToken, string serverId)
        {
            string uri = "https://discord.com/api/guilds/" + serverId;
            // Remember to handle errors here
            string response = await GetJsonStringFromEndpointForBot(botToken, uri);
            // And here

            return response;
        }    
    }
}