// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoSkillBot v4.7.0

using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using AdaptiveCards;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Builder.Teams;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Bot.Schema;
using Microsoft.Bot.Schema.Teams;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SkillBot.Bots
{
    public class EchoBot : TeamsActivityHandler
    {
        IConfiguration _configuration;

        public EchoBot(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private Attachment GetTaskModuleHeroCard()
        {
            return new HeroCard()
            {
                Title = $"Task Module Invocation {_configuration["MicrosoftAppId"]}",
                Subtitle = "This is a hero card with a Task Module Action button.  Click the button to show an Adaptive Card within a Task Module.",
                Buttons = new List<CardAction>()
                    {
                         new TaskModuleAction("Show Task Module", JObject.Parse("{ \"type\" : \"TaskModule invoke\", \"skillid\" : \"" + _configuration["MicrosoftAppId"] + "\" }")),
                    },
            }.ToAttachment();
        }

        private Attachment CreateAdaptiveCardAttachment(bool chooseDifferentColor = false)
        {
            var adaptiveCard = new AdaptiveCard();
            adaptiveCard.Body.Add(new AdaptiveTextBlock("Adaptive Card in Task Module"));
            adaptiveCard.Body.Add(new AdaptiveTextBlock(_configuration["MicrosoftAppId"]));
            if (chooseDifferentColor)
            {
                adaptiveCard.Body.Add(new AdaptiveTextBlock("Choose a different color!") { Weight = AdaptiveTextWeight.Bolder });
            }
            adaptiveCard.Body.Add(new AdaptiveChoiceSetInput()
            {
                Id = "color",
                IsMultiSelect = false,
                Value = "Red",
                Choices = new List<AdaptiveChoice>()
                {
                    new AdaptiveChoice() { Title = "Blue", Value = "Blue" },
                    new AdaptiveChoice() { Title = "Red", Value = "Red" },
                }
            });
            adaptiveCard.Actions.Add(new AdaptiveSubmitAction() { Title = "Close", Data = JObject.Parse("{ \"type\" : \"TaskModule invoke\", \"skillid\" : \"" + _configuration["MicrosoftAppId"] + "\" }") });
            return new Attachment
            {
                Content = adaptiveCard,
                ContentType = AdaptiveCards.AdaptiveCard.ContentType,
            };
        }

        private TaskModuleResponse CreateTaskModuleContinueResponse(bool chooseDifferentColor = false)
        {
            return new TaskModuleResponse
            {
                Task = new TaskModuleContinueResponse
                {
                    Value = new TaskModuleTaskInfo()
                    {
                        Card = CreateAdaptiveCardAttachment(chooseDifferentColor),
                        Height = 200,
                        Width = 400,
                        Title = "Adaptive Card: Inputs",
                    },
                },
            };
        }

        protected override async Task OnMessageActivityAsync(ITurnContext<IMessageActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Text.Contains("end") || turnContext.Activity.Text.Contains("stop"))
            {
                // Send End of conversation at the end.
                await turnContext.SendActivityAsync(MessageFactory.Text($"ending conversation from the skill..."), cancellationToken);
                var endOfConversation = Activity.CreateEndOfConversationActivity();
                endOfConversation.Code = EndOfConversationCodes.CompletedSuccessfully;
                await turnContext.SendActivityAsync(endOfConversation, cancellationToken);
            }
            else
            {
                await turnContext.SendActivityAsync(MessageFactory.Text($"{_configuration["MicrosoftAppId"]} Echo (dotnet) : {turnContext.Activity.Text}"), cancellationToken);
                await turnContext.SendActivityAsync(MessageFactory.Text("2nd message sent from Echo Skill. :)"), cancellationToken);
            }
        }
        protected override async Task<InvokeResponse> OnTeamsCardActionInvokeAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            await turnContext.SendActivityAsync(MessageFactory.Text($"{_configuration["MicrosoftAppId"]}: OnTeamsCardActionInvokeAsync."));
            var reply = MessageFactory.Attachment(this.GetTaskModuleHeroCard());
            await turnContext.SendActivityAsync(reply);

            return new InvokeResponse() { Status = (int)HttpStatusCode.OK };
        }

        protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
        {
            if (turnContext.Activity.Name == "CustomHealthCheck")
            {
                var token = await (turnContext.Adapter as SkillAdapterWithErrorHandler).GetBotSkillToken(turnContext);

                return new InvokeResponse()
                {
                    Status = (int)HttpStatusCode.OK,
                    Body = JObject.FromObject(new
                    {
                        custom_data_type = new
                        {
                            title = "Bot Framework",
                            link = "http://dev.botframework.com",
                        },
                        bot_token = token
                    })
                };
            }

            return await base.OnInvokeActivityAsync(turnContext, cancellationToken);
        }

        protected override async Task<TaskModuleResponse> OnTeamsTaskModuleFetchAsync(ITurnContext<IInvokeActivity> turnContext, TaskModuleRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            var reply = MessageFactory.Text("OnTeamsTaskModuleFetchAsync TaskModuleRequest: " + JsonConvert.SerializeObject(taskModuleRequest));
            await turnContext.SendActivityAsync(reply);

            return CreateTaskModuleContinueResponse();
        }

        protected override async Task<TaskModuleResponse> OnTeamsTaskModuleSubmitAsync(ITurnContext<IInvokeActivity> turnContext, TaskModuleRequest taskModuleRequest, CancellationToken cancellationToken)
        {
            var reply = MessageFactory.Text("OnTeamsTaskModuleSubmitAsync Value: " + JsonConvert.SerializeObject(taskModuleRequest));
            await turnContext.SendActivityAsync(reply);

            if((taskModuleRequest.Data as JObject).ContainsKey("color"))
            {
                if ((taskModuleRequest.Data as JObject).Value<string>("color") == "Blue")
                {
                    return CreateTaskModuleContinueResponse(true);
                }
            }

            return new TaskModuleResponse
            {
                Task = new TaskModuleMessageResponse()
                {
                    Value = "Thanks!",
                },
            };
        }

        protected override async Task OnMembersAddedAsync(IList<ChannelAccount> membersAdded, ITurnContext<IConversationUpdateActivity> turnContext, CancellationToken cancellationToken)
        {
            var welcomeText = "Hello and welcome to EchoBot!";
            foreach (var member in membersAdded)
            {
                if (member.Id != turnContext.Activity.Recipient.Id)
                {
                    await turnContext.SendActivityAsync(MessageFactory.Text(welcomeText, welcomeText), cancellationToken);
                }
            }
        }
    }
}
