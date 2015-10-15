﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using DwellerBot.Models;
using Newtonsoft.Json;
using Telegram.Bot;
using Telegram.Bot.Types;
using Serilog;

namespace DwellerBot.Commands
{
    class RateNbrbCommand: CommandBase
    {
        private const string CurrencyQueryUrl = @"http://www.nbrb.by/Services/XmlExRates.aspx";
        private const string OnDateParam = @"?ondate=";
        private readonly List<string> _defaultCurrenciesList  = new List<string> {"USD","EUR","RUB"};

        private CurrencyContainerXml _previousDayCurrencyContainer;

        public RateNbrbCommand(Api bot):base(bot)
        {
            ;
        }

        public override async Task ExecuteAsync(Update update, Dictionary<string, string> parsedMessage)
        {
            var responseStream = new StreamReader(await GetCurrencyRates());
            var xmlDeserializer = new XmlSerializer(typeof(CurrencyContainerXml.DailyExRates));
            var currencyContainer = new CurrencyContainerXml() { DailyRates = xmlDeserializer.Deserialize(responseStream) as CurrencyContainerXml.DailyExRates};

            if (_previousDayCurrencyContainer == null ||
                DateTime.ParseExact(_previousDayCurrencyContainer.DailyRates.Date, "MM/dd/yyyy", null).AddDays(1) !=
                DateTime.ParseExact(currencyContainer.DailyRates.Date, "MM/dd/yyyy", null))
            {
                var ondate = DateTime.ParseExact(currencyContainer.DailyRates.Date, "MM/dd/yyyy", null).AddDays(-1).ToString(@"MM\/dd\/yyyy");
                responseStream = new StreamReader(await GetCurrencyRates(OnDateParam + ondate));
                _previousDayCurrencyContainer = new CurrencyContainerXml() { DailyRates = xmlDeserializer.Deserialize(responseStream) as CurrencyContainerXml.DailyExRates };
            }

            var sb = new StringBuilder();
            sb.Append("Курсы валют на ");
            sb.AppendLine(DateTime.ParseExact(currencyContainer.DailyRates.Date, "MM/dd/yyyy", null).ToShortDateString());
            sb.AppendLine();

            List<string> currenciesList = new List<string>();
            if (parsedMessage.ContainsKey("message"))
            {
                var names = parsedMessage["message"].Split(',').ToList();
                currenciesList.AddRange(names.Select(cname => cname.ToUpper()));
            }
            if (currenciesList.Count == 0)
                currenciesList = _defaultCurrenciesList;

            foreach (var currency in currencyContainer.DailyRates.Currency.Where(x => currenciesList.Contains(x.CharCode)))
            {
                sb.Append(currency.CharCode + ": " + currency.Rate);
                if (_previousDayCurrencyContainer != null)
                {
                    var diff = currency.Rate -
                               _previousDayCurrencyContainer.DailyRates.Currency.First(
                                   x => x.CharCode == currency.CharCode).Rate;
                    sb.Append(" _(");
                    sb.Append(diff > 0 ? "+" : "-");
                    sb.Append(Math.Abs(diff));
                    sb.Append(")_");
                }
                sb.AppendLine();
            }

            await _bot.SendTextMessage(update.Message.Chat.Id, sb.ToString(), false, update.Message.MessageId, null, true);
        }

        public async Task<Stream> GetCurrencyRates(string param = "")
        {
            var hc = new HttpClient();
            return await hc.GetStreamAsync(CurrencyQueryUrl + param);
        }
    }
}
