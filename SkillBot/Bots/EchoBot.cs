// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.
//
// Generated with Bot Builder V4 SDK Template for Visual Studio EchoSkillBot v4.7.0

using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Schema;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace SkillBot.Bots
{
    public class EchoBot : ActivityHandler
    {
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
                await turnContext.SendActivityAsync(MessageFactory.Text($"Echo (dotnet) : {turnContext.Activity.Text}"), cancellationToken);
                await turnContext.SendActivityAsync(MessageFactory.Text("2nd message sent from Echo Skill. :)"), cancellationToken);
            }
        }

        protected override async Task<InvokeResponse> OnInvokeActivityAsync(ITurnContext<IInvokeActivity> turnContext, CancellationToken cancellationToken)
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
