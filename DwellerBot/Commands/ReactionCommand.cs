﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DwellerBot.Utility;
using Telegram.Bot;
using Telegram.Bot.Types;
using Newtonsoft.Json;

namespace DwellerBot.Commands
{
    class ReactionCommand : ICommand, ISaveable
    {
        private readonly Api _bot;
        private readonly List<FileInfo> _files;
        private readonly Random _rng;
        private readonly string _cacheFilePath;
        private Dictionary<string, string> _sentFiles;

        public ReactionCommand(Api bot, List<string> folderNames, string cacheFilePath)
        {
            _bot = bot;
            _rng = new Random();
            _files = new List<FileInfo>();
            foreach (var folderName in folderNames)
            {
                var dir = new DirectoryInfo(folderName);
                if (dir.Exists)
                {
                    _files.AddRange(dir.EnumerateFiles().ToList());
                }
            }
            _cacheFilePath = cacheFilePath;
            _sentFiles = new Dictionary<string, string>();
        }

        public void Execute(Update update)
        {
            throw new NotImplementedException();
        }

        public async Task ExecuteAsync(Update update, Dictionary<string, string> parsedMessage)
        {
            if (_files.Count == 0)
            {
                await
                    _bot.SendTextMessage(update.Message.Chat.Id, "No files available.", false, update.Message.MessageId);
                return;
            }

            var ind = _rng.Next(0, _files.Count);
            if (_sentFiles.ContainsKey(_files[ind].Name))
            {
                try
                {
                    await _bot.SendPhoto(update.Message.Chat.Id, _sentFiles[_files[ind].Name], "", update.Message.MessageId);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("An error has occured during file resending! Message: {0}", ex.Message));
                }
                return;
            }

            using (var fs = new FileStream(_files[ind].FullName, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    var message = await _bot.SendPhoto(update.Message.Chat.Id, new FileToSend(_files[ind].Name, fs ), "", update.Message.MessageId);
                    _sentFiles.Add(_files[ind].Name, message.Photo.Last().FileId);
                }
                catch (Exception ex)
                {
                    Logger.Error(string.Format("An error has occured during file sending! Message: {0}", ex.Message));
                }
            }
        }

        public void SaveState()
        {
            if (_sentFiles.Count == 0)
                return;

            using (var sw = new StreamWriter(new FileStream(_cacheFilePath, FileMode.Create, FileAccess.Write)))
            {
                var config = JsonConvert.SerializeObject(_sentFiles);
                sw.WriteLine(config);
            }

            Logger.Info("ReactionCommand state was successfully saved.");
        }

        public void LoadState()
        {
            using (var sr = new StreamReader(new FileStream(_cacheFilePath, FileMode.OpenOrCreate)))
            {
                var str = sr.ReadToEnd();
                if (str.Length > 0)
                {
                    Dictionary<string, string> config = null;
                    try
                    {
                        config = JsonConvert.DeserializeObject<Dictionary<string, string>>(str);
                    }
                    catch (Exception ex)
                    {
                        Logger.Error(string.Format("An error as occured during parsing of {0} file. Message: {1}", _cacheFilePath, ex.Message));
                    }
                    if (config != null)
                        _sentFiles = config;
                }
                else
                {
                    Logger.Warning(string.Format("The file {0} was expected to be populated with data, but was empty.", _cacheFilePath));
                }
            }
        }
    }
}
