// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio SkillRootBot v4.7.1

using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Integration.AspNet.Core.Skills;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace ParentBot.Controllers
{
    // http://localhost:3978/api/test?type=message&deliveryMode=expectReplies
    // http://localhost:3978/api/test?type=invoke

    [Route("api/test")]
    [ApiController]
    public class TestController : ControllerBase
    {
        private readonly string _botId;
        private readonly SkillHttpClient _skillClient;
        private readonly SkillsConfiguration _skillsConfig;

        public TestController(ConversationState conversationState, SkillsConfiguration skillsConfig, SkillHttpClient skillClient, IConfiguration configuration)
        {
            _skillsConfig = skillsConfig ?? throw new ArgumentNullException(nameof(skillsConfig));
            _skillClient = skillClient ?? throw new ArgumentNullException(nameof(skillsConfig));
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            _botId = configuration.GetSection(MicrosoftAppCredentials.MicrosoftAppIdKey)?.Value;
            if (string.IsNullOrWhiteSpace(_botId))
            {
                throw new ArgumentException($"{MicrosoftAppCredentials.MicrosoftAppIdKey} is not set in configuration");
            }
        }

        [HttpPost, HttpGet]
        public async Task PostAsync()
        {
            var skill = _skillsConfig.Skills.First().Value;

            var activityToSend = MessageFactory.Text("test");
            activityToSend.Conversation = new ConversationAccount() { Id = Guid.NewGuid().ToString() };
            activityToSend.From = new ChannelAccount() { Id = _botId };
            activityToSend.Recipient = new ChannelAccount() { Id = skill.Id };
            activityToSend.Type = ActivityTypes.Invoke;
            activityToSend.Name = "health";

            if (Request.Query != null )
            {
                if (Request.Query.ContainsKey("deliveryMode"))
                {
                    activityToSend.DeliveryMode = Request.Query["deliveryMode"];
                }

                if (Request.Query.ContainsKey("type"))
                {
                    activityToSend.Type = Request.Query["type"];
                }
            }

            await SendToSkill(Response, skill, activityToSend); 
        }

        private async Task SendToSkill(HttpResponse response, BotFrameworkSkill targetSkill, Activity activity, CancellationToken cancellationToken = default)
        {
            if (activity.Type == ActivityTypes.Invoke)
            {
                var invokeResponse = await PostActivityToSkill<JObject>(targetSkill, activity, cancellationToken);

                if (invokeResponse != null)
                {
                    response.ContentType = "text/html";
                    response.StatusCode = (int)HttpStatusCode.OK;
                    string text = $"<html><body>{System.Web.HttpUtility.HtmlEncode(invokeResponse.ToString())}</body></html>";
                    await HttpResponseWritingExtensions.WriteAsync(response, text);
                }
            }
            else
            {
                var expectedReplies = await PostActivityToSkill<ExpectedReplies>(targetSkill, activity, cancellationToken);

                if (expectedReplies != null && activity.DeliveryMode == DeliveryModes.ExpectReplies)
                {
                    string responseText = string.Empty;
                    foreach (var a in expectedReplies.Activities)
                    {
                        responseText += $"<p>ExpectedReplies: {a.Text}</p>";
                    }

                    response.ContentType = "text/html";
                    response.StatusCode = (int)HttpStatusCode.OK;
                    string text = $"<html><body>{responseText}</body></html>";
                    await HttpResponseWritingExtensions.WriteAsync(response, text);
                }
            }
        }

        private async Task<T> PostActivityToSkill<T>(BotFrameworkSkill targetSkill, Activity activity, CancellationToken cancellationToken)
        {
            // route the activity to the skill
            var response = await _skillClient.PostActivityAsync<T>(_botId, targetSkill, _skillsConfig.SkillHostEndpoint, activity, cancellationToken);

            // Check response status
            if (!(response.Status >= 200 && response.Status <= 299))
            {
                throw new HttpRequestException($"Error invoking the skill id: \"{targetSkill.Id}\" at \"{targetSkill.SkillEndpoint}\" (status is {response.Status}). \r\n {response.Body}");
            }

            return response.Body;
        }
    }
}
