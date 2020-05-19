// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio SkillRootBot v4.7.1

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Integration.AspNet.Core.Skills;
using Microsoft.Bot.Builder.Skills;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace ParentBot.Bots
{
    public class RootBot : TeamsActivityHandler
    {
        private readonly string _botId;
        private readonly ConversationState _conversationState;
        private readonly SkillHttpClient _skillClient;
        private readonly SkillsConfiguration _skillsConfig;

        public RootBot(ConversationState conversationState, SkillsConfiguration skillsConfig, SkillHttpClient skillClient, IConfiguration configuration)
        {
            _conversationState = conversationState ?? throw new ArgumentNullException(nameof(conversationState));
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

        private JObject GetCardActionValue(string type, string skillId)
        {
            return JObject.Parse("{ \"type\" : \"" + type + "\", \"skillid\" : \"" + skillId + "\" }");
        }

        private IEnumerable<Attachment> GetOptionsAttachment()
        {
            var attachments = new List<Attachment>();
            if (_skillsConfig.Skills.Count > 0)
            {
                foreach (var skill in _skillsConfig.Skills)
                {
                    var buttons = new List<CardAction>();

                    // for testing synchronous operations
                    buttons.Add(new CardAction(ActionTypes.MessageBack, title: $"MessageBack invoke to skill", text: "invoke skill", displayText: "MessageBack to invoke skill", value: GetCardActionValue("MessageBack invoke", skill.Value.Id)));
                    buttons.Add(new CardAction(ActionTypes.MessageBack, title: $"MessageBack expectReplies to skill", text: "MessageBack expectReplies", displayText: "MessageBack to expectReplies skill", value: GetCardActionValue("MessageBack expectReplies", skill.Value.Id)));
                    buttons.Add(new CardAction(ActionTypes.MessageBack, title: $"MessageBack teams adaptive card", text: "teams adaptive card", displayText: "MessageBack get teams adaptive card", value: GetCardActionValue("MessageBack get adaptive card", skill.Value.Id)));
                    buttons.Add(new CardAction("invoke", title: "invoke to invoke skill", null, text: "invoke", displayText: "invoke to invoke skill", value: GetCardActionValue("invoke invoke", skill.Value.Id)));

                    var heroCard = new HeroCard
                    {
                        Title = $"Skills Options for {skill.Value.Id}-{skill.Value.AppId}",
                        Text = "Click one of the buttons below to initiate that skill.",
                        Buttons = buttons
                    };
                    attachments.Add(heroCard.ToAttachment());
                }
            }
            else
            {
                attachments.Add(new HeroCard
                {
                    Title = "No Skills configured...",
                    Subtitle = "Configure some skills in appsettings.json",
                }.ToAttachment());
            }

            return attachments;
        }

        private Attachment GetAdaptiveCardWithInvokeAction(Activity activity)
        {
            var skillId = ((JObject)activity.Value).Value<string>("skillid");
            var skill = _skillsConfig.Skills[skillId];

            var adaptiveCard = new AdaptiveCard();
            adaptiveCard.Body.Add(new AdaptiveTextBlock("Bot Builder Invoke Action"));
            var action4 = new CardAction("invoke", "custom health check invoke", null, null, null, GetCardActionValue("Adaptive Card invoke", skill.Id));
            adaptiveCard.Actions.Add(action4.ToAdaptiveCardAction());

            return adaptiveCard.ToAttachment();
        }

        private async Task<object> SendToSkill(ITurnContext turnContext, BotFrameworkSkill targetSkill, Activity activity = null, CancellationToken cancellationToken = default)
        {
            // NOTE: Always SaveChanges() before calling a skill so that any activity generated by the skill
            // will have access to current accurate state.
            await _conversationState.SaveChangesAsync(turnContext, force: true, cancellationToken: cancellationToken);

            var activityToSend = activity ?? (Activity)turnContext.Activity;

            if (activityToSend.Type == ActivityTypes.Invoke)
            {
                var invokeResponse = await PostActivityToSkill<object>(targetSkill, activityToSend, cancellationToken);

                if (invokeResponse != null)
                {
                    if(activity.Name == "task/fetch" || activity.Name == "task/submit")
                    {
                        return invokeResponse;
                    }
                    await turnContext.SendActivityAsync("Received Invoke Response Body: " + (invokeResponse as JObject).ToString());
                }
            }
            else
            {
                var expectedReplies = await PostActivityToSkill<ExpectedReplies>(targetSkill, activityToSend, cancellationToken);

                if (expectedReplies != null && activity.DeliveryMode == DeliveryModes.ExpectReplies)
                {
                    // sending messages back requires the turnContext.Activity.DeliveryMode to NOT be ExpectReplies, so change it back
                    turnContext.Activity.DeliveryMode = DeliveryModes.Normal;
                    foreach (var a in expectedReplies.Activities)
                    {
                        a.Text = $"ExpectedReplies: {a.Text}";
                        await turnContext.SendActivityAsync(a);
                    }
                }
            }

            return null;
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

        protected override async Task<InvokeResponse> OnTeamsCardActionInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text("Calling skill from OnTeamsCardActionInvokeAsync."));

            if(((JObject)turnContext.Activity.Value).ContainsKey("type") 
                && ((JObject)turnContext.Activity.Value).Value<string>("type") == "Adaptive Card invoke")
            {
                turnContext.Activity.Name = "CustomHealthCheck";
            }

            await SendToSkill(turnContext, GetSkillFromValue(turnContext.Activity.Value), turnContext.Activity as Activity, cancellationToken);

            return new InvokeResponse() { Status = (int)HttpStatusCode.OK };
        }

        protected override async Task<TaskModuleResponse> OnTeamsTaskModuleFetchAsync(ITurnContext<IInvokeActivity> turnContext, TaskModuleRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            var result = await SendToSkill(turnContext, GetSkillFromValue(turnContext.Activity.Value), turnContext.Activity as Activity, cancellationToken);

            var taskModuleResponse = ((JObject)result).Value<JObject>("task");
            var continueResponse = taskModuleResponse.ToObject<TaskModuleContinueResponse>();
            return new TaskModuleResponse(continueResponse);
        }

        private BotFrameworkSkill GetSkillFromValue(object value)
        {
            string skillId = null;
            var valueAsJObject = ((JObject)value);
            if (valueAsJObject.ContainsKey("skillid"))
            {
                skillId = valueAsJObject.Value<string>("skillid");
                return _skillsConfig.Skills[skillId];
            }
            else
            {
                // no skillid, so look for 'data' (in the case of TaskModuleFetch, custom properties are under 'data')
                JToken dataToken = null;
                if(valueAsJObject.TryGetValue("data",out dataToken))
                {
                    if (dataToken.Type.ToString() == "String")
                    {
                        var asJobject = JObject.Parse(dataToken.ToString());
                        skillId = asJobject.Value<string>("skillid");
                    }
                    else
                    {
                        skillId = dataToken.Value<string>("skillid");
                    }
                }
                else
                {
                    var data = valueAsJObject.Value<JObject>("data");
                    skillId = data.Value<string>("skillid");
                }
                return _skillsConfig.Skills.First(s=>s.Value.AppId == skillId).Value;
            }
        }

        protected override async Task<TaskModuleResponse> OnTeamsTaskModuleSubmitAsync(ITurnContext<IInvokeActivity> turnContext, TaskModuleRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            var result = await SendToSkill(turnContext, GetSkillFromValue(turnContext.Activity.Value), turnContext.Activity as Activity, cancellationToken);

            var taskModuleResponse = ((JObject)result).Value<JObject>("task");
            var taskType = taskModuleResponse["type"].Value<string>();
            if(taskType == "message")
            {
                var messageResponse = taskModuleResponse.ToObject<TaskModuleMessageResponse>();
                return new TaskModuleResponse(messageResponse);
            }

            var continueResponse = taskModuleResponse.ToObject<TaskModuleContinueResponse>();
            return new TaskModuleResponse(continueResponse);
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            var text = turnContext.Activity.Text;

            if (!string.IsNullOrEmpty(text))
            {
                if (text.Contains("teams"))
                {
                    var reply = MessageFactory.Attachment(GetAdaptiveCardWithInvokeAction(turnContext.Activity as Activity));
                    await turnContext.SendActivityAsync(reply, cancellationToken);
                    return;
                }

                var activity = JsonConvert.DeserializeObject<Activity>(JsonConvert.SerializeObject(turnContext.Activity, HttpHelper.BotMessageSerializerSettings));
                if (text.Contains("invoke"))
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Got it, connecting you to the skill using {text}..."), cancellationToken);
                    // HACK the current message, wich is being fwded to the child bot, 
                    // and force the child bot to respond synchronously by changing the message type to Invoke
                    activity.Type = ActivityTypes.Invoke;
                }
                else if (text.Contains("expectReplies"))
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"Got it, connecting you to the skill using {text}..."), cancellationToken);
                    // HACK the current message, wich is being fwded to the child bot, 
                    // and force the child bot to respond synchronously via ExpectReplies
                    activity.DeliveryMode = DeliveryModes.ExpectReplies;
                }
                if (activity.Value == null || !((JObject)activity.Value).ContainsKey("skillid"))
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text($"You said: {text}... this will NOT trigger a skill"), cancellationToken);
                }
                else
                {
                    // Send the activity to the skill
                    var skillId = ((JObject)activity.Value).Value<string>("skillid");
                    var skill = _skillsConfig.Skills[skillId];
                    await SendToSkill(turnContext, skill, activity, cancellationToken);
                }
            }

            // just respond with choices
            await turnContext.SendActivityAsync(MessageFactory.Attachment(GetOptionsAttachment()), cancellationToken);

            // Save conversation state
            await _conversationState.SaveChangesAsync(turnContext, force: true, cancellationToken: cancellationToken);
        }


        protected override async Task OnEndOfConversationActivityAsync(ITurnContext<IEndOfConversationActivity> turnContext, CancellationToken cancellationToken)
        {
            // forget skill invocation
            //await _activeSkillProperty.DeleteAsync(turnContext, cancellationToken);

            // Show status message, text and value returned by the skill
            var eocActivityMessage = $"Received {ActivityTypes.EndOfConversation}.\n\nCode: {turnContext.Activity.Code}";
            if (!string.IsNullOrWhiteSpace(turnContext.Activity.Text))
            {
                eocActivityMessage += $"\n\nText: {turnContext.Activity.Text}";
            }

            if ((turnContext.Activity as Activity)?.Value != null)
            {
                eocActivityMessage += $"\n\nValue: {JsonConvert.SerializeObject((turnContext.Activity as Activity)?.Value)}";
            }

            await turnContext.SendActivityAsync(MessageFactory.Text(eocActivityMessage), cancellationToken);

            // We are back at the root
            await turnContext.SendActivityAsync(MessageFactory.Text("Back in the root dotnet bot."), cancellationToken);
            await turnContext.SendActivityAsync(MessageFactory.Attachment(GetOptionsAttachment()), cancellationToken);


            // Save conversation state
            await _conversationState.SaveChangesAsync(turnContext, cancellationToken: cancellationToken);
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text("Hello and welcome!"), cancellationToken);
                }
            }
        }
    }
}
