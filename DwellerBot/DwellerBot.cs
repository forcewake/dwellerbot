﻿using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using DwellerBot.Commands;
using Telegram.Bot;
using Telegram.Bot.Types;
using Serilog;
using DwellerBot.Services;

namespace DwellerBot
{
    public class DwellerBot
    {
        internal const string BotName = @"@DwellerBot";
        internal const string OwnerUsername = "angelore";
        internal const int OwnerId = 99541817;

        private readonly Api _bot;

        private Random _rng;

        internal int Offset;
        internal DateTime LaunchTime;
        internal int CommandsProcessed;
        internal int ErrorCount;
        internal bool IsOnline = true;
        
        internal CommandService CommandService { get; }

        public DwellerBot(Settings settings)
        {
            // move to container
            CommandService = new CommandService(BotName);

            _rng = new Random();

            // Get bot api token
            _bot = new Api(settings.keys.First(x => x.name == "dwellerBotKey").value);

            Offset = 0;
            CommandsProcessed = 0;
            ErrorCount = 0;
            LaunchTime = DateTime.Now.AddHours(3);

            CommandService.RegisterCommands(new Dictionary<string, ICommand>
            {
                {@"/debug", new DebugCommand(_bot, this)},
                {@"/rate", new RateNbrbCommand(_bot)},
                {@"/askstason", new AskStasonCommand(_bot, settings.paths.paths.First(x => x.name == "askStasonResponsesPath").value)},
                {@"/weather", new WeatherCommand(_bot, settings.keys.First(x => x.name == "openWeatherKey").value)},
                {
                    @"/reaction",
                    new ReactionCommand(
                        _bot,
                        settings.paths.pathGroups.First(x => x.name == "reactionImagePaths").paths.Select(x => x.value).ToList(),
                        settings.paths.paths.First(x => x.name == "reactionImageCachePath").value
                        )
                },
                {@"/rtd", new RtdCommand(_bot)},
                {@"/featurerequest", new FeatureRequestCommand(_bot, settings.paths.paths.First(x => x.name == "featureRequestsPath").value)},
                {@"/bash", new BashimCommand(_bot)},
                {@"/savestate", new SaveStateCommand(_bot, this)},
                {@"/shutdown", new ShutdownCommand(_bot, this)}
            });

            CommandService.LoadCommandStates();
        }
        
        public async Task Run()
        {
            var me = await _bot.GetMe();
            
            Log.Logger.Information("{0} is online and fully functional." + Environment.NewLine, me.Username);

            while (IsOnline)
            {
                Update[] updates = new Update[0];
                try
                {
                    updates = await _bot.GetUpdates(Offset);
                }
                catch (Exception ex)
                {
                    Log.Logger.Error("An error has occured while receiving updates. Error message: {0}", ex.Message);
                    ErrorCount++;
                }

                List<Task> tasks = new List<Task>();
                foreach (var update in updates)
                {
                    //var updateTask = Task.Factory.StartNew(() => CommandService.HandleUpdate(update));
                    var updateTask = CommandService.HandleUpdate(update);
                    tasks.Add(updateTask);

                    Offset = update.Id + 1;
                }
                Task.WaitAll(tasks.ToArray());

                await Task.Delay(1000);
            }
        }

        public static bool IsUserOwner(User user)
        {
            if (user.Id == OwnerId && user.Username.Equals(OwnerUsername))
                return true;

            return false;
        }        
    }
}
