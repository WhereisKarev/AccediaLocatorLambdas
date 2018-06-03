using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using F23.StringSimilarity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AccediaLocator
{
    public class UserIsInOfficeFunction
    {
        #region Constants
        private const string _TableName = "accedia-locator-users";
        #endregion

        #region Function handler
        /// <summary>
        /// The function handler which is called, when the lambda is being called
        /// </summary>
        /// <param name="input">The skill request, comming from Alexa</param>
        /// <param name="context">The lambda context</param>
        /// <returns>Skill response for Alexa to answer</returns>
        public async Task<SkillResponse> FunctionHandler(SkillRequest input, ILambdaContext context)
        {
            var logger = context.Logger;

            switch (input.Request)
            {
                case LaunchRequest launchRequest: return HandleLaunch(launchRequest, logger);
                case IntentRequest intentRequest: return await HandleIntent(input, logger);
            }

            throw new NotImplementedException("Unknown request type.");
        }
        #endregion

        #region Constructor
        public UserIsInOfficeFunction()
        {
            _isPersonHerePositiveResponses = InitializeIsPersonHerePositiveResponses();
            _isPersonHereNegativeResponses = InitializeIsPersonHereNegativeResponses();
            _whereIsPersonResponses = InitializeWhereIsPersonResponses();
            _somewhereInOfficeResponses = InitializeSomewhereInOfficeResponses();
            _isPersonHereTodayResponses = InitializePersonHereTodayResponses();
            _isPersonNotHereTodayResponses = InitializePersonNotHereTodayResponses();
        }
        #endregion

        #region Responses pool
        private List<string> _isPersonHerePositiveResponses;
        private List<string> _isPersonHereNegativeResponses;
        private List<string> _whereIsPersonResponses;
        private List<string> _somewhereInOfficeResponses;
        private List<string> _isPersonHereTodayResponses;
        private List<string> _isPersonNotHereTodayResponses;

        private List<string> InitializeWhereIsPersonResponses()
        {
            List<string> responses = new List<string>();

            responses.Add("{0} is in room {1}");
            responses.Add("I have found {0} in room {1}");
            responses.Add("{0} is currently in room {1}");
            responses.Add("{0} can be found in room {1}");
            responses.Add("{0} is probably doing nothing in room {1}");
            responses.Add("{0} is probably doing nothing in room {1}, as usual.");
            responses.Add("{0} is annoying the people in room {1}");

            return responses;
        }

        private List<string> InitializeIsPersonHereNegativeResponses()
        {
            List<string> responses = new List<string>();

            responses.Add("Nope, {0} is not here.");
            responses.Add("This {0} person, you are looking for is not here.");
            responses.Add("{0}, is not here.");
            responses.Add("{0} is not in the office.");

            return responses;
        }

        private List<string> InitializeIsPersonHerePositiveResponses()
        {
            List<string> responses = new List<string>();

            responses.Add("Yep, {0} is here.");
            responses.Add("Yes, {0} is here.");
            responses.Add("Yes, {0} has arrived at the office.");
            responses.Add("This {0} person, you are looking for is here.");
            responses.Add("{0}, is here.");
            responses.Add("{0} is in the office, yes.");
            responses.Add("Yep, {0} came to work today.");

            return responses;
        }

        private List<string> InitializeSomewhereInOfficeResponses()
        {
            return new List<string>
            {
                "{0} is somewhere in the office",
                "{0} jumps to often from room to room in the office to track him",
                "{0} is probably sleeping again somewhere in the office. Don't interrupt him!",
                "I guess that {0} is running around the office as usual"
            };
        }

        private List<string> InitializePersonNotHereTodayResponses()
        {
            return new List<string>
            {
                "{0} does not intend to come to work today",
                "I guess that {0} is still sleeping home",
                "{0} is not here today",
                "{0} has not come to the office yet"
            };
        }

        private List<string> InitializePersonHereTodayResponses()
        {
            return new List<string>
            {
                "Yes, {0} is in the office today",
                "{0} came to work today, probably, but he did not accept my privacy policy to allow me track him in the office"
            };
        }

        private string GetResponse(List<string> responses, params string[] pars)
        {
            Random rand = new Random();
            int index = rand.Next(0, responses.Count);

            return string.Format(responses[index], pars);
        }

        #endregion

        #region Handle alexa requests
        /// <summary>
        /// Handles the intent and returns the required information
        /// </summary>
        private async Task<SkillResponse> HandleIntent(SkillRequest skillRequest, ILambdaLogger logger)
        {
            IntentRequest intentRequest = skillRequest.Request as IntentRequest;

            if (intentRequest.Intent.ConfirmationStatus == ConfirmationStatus.Denied)
            {
                return ResponseBuilder.Tell("Then I cannot help you!");
            }

            if (intentRequest.Intent.Name == "PersonIsInOfficeIntent")
            {
                return await HandleIsPersonInOfficeIntent(intentRequest, logger);
            }
            else if (intentRequest.Intent.Name == "WhereIsPersonIntent")
            {
                return await HandlewhereIsPersonIntent(intentRequest, logger);
            }
            else if (intentRequest.Intent.Name == "PersonCameToWorkToday")
            {
                return await HandlePersonCameToWorkToday(skillRequest, logger);
            }
            else
            {
                return HandleUnknownIntent();
            }
        }

        private async Task<SkillResponse> HandlewhereIsPersonIntent(IntentRequest intentRequest, ILambdaLogger logger)
        {
            logger.LogLine($"IntentRequest {intentRequest.Intent.Name} made");

            string responseSpeech = "Sorry, I don't know this person.";

            if (intentRequest.Intent.Slots.TryGetValue("name", out var nameSlot))
            {
                string name = nameSlot.Value.ToLower();

                logger.LogLine($"Looking for person with name: {name}");

                // Create  dynamodb client  
                var dynamoDbClient = new AmazonDynamoDBClient(
                    new AmazonDynamoDBConfig
                    {
                        RegionEndpoint = RegionEndpoint.USEast1
                    });

                var request = new GetItemRequest
                {
                    TableName = _TableName,
                    Key = new Dictionary<string, AttributeValue>() { { "Username", new AttributeValue { S = name } } },
                };
                var dbResponse = await dynamoDbClient.GetItemAsync(request);

                // Check the response.
                var result = dbResponse.Item;

                if ((result != null) && (result.Count != 0))
                {
                    string room = result["Room"].S.ToLowerInvariant();
                    bool isInOffice = result["IsInOffice"].BOOL;

                    if (isInOffice)
                    {
                        if (room == "somewhere")
                        {
                            responseSpeech = GetResponse(_somewhereInOfficeResponses, name);
                        }
                        else
                        {
                            responseSpeech = GetResponse(_whereIsPersonResponses, name, room);
                        }
                    }
                    else
                    {
                        responseSpeech = $"{name} has not arrived at the office yet.";
                    }
                }
                else
                {
                    var unkknownUserResponse = await HandleUnknownUser(intentRequest, dynamoDbClient, name);
                    if (unkknownUserResponse != null)
                    {
                        return unkknownUserResponse;
                    }
                }
            }

            var response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
            {
                Text = responseSpeech
            });

            return response;
        }

        private SkillResponse HandleUnknownIntent()
        {
            var response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
            {
                Text = "Sorry, I didn't get that."
            });

            return response;
        }

        private async Task<SkillResponse> HandleIsPersonInOfficeIntent(IntentRequest intentRequest, ILambdaLogger logger)
        {
            logger.LogLine($"IntentRequest {intentRequest.Intent.Name} made");

            string responseSpeech = "Sorry, I don't know this person.";

            if (intentRequest.Intent.Slots.TryGetValue("name", out var nameSlot))
            {
                string name = nameSlot.Value.ToLower();

                logger.LogLine($"Looking for person with name: {name}");

                // Create  dynamodb client  
                var dynamoDbClient = new AmazonDynamoDBClient(
                    new AmazonDynamoDBConfig
                    {
                        RegionEndpoint = RegionEndpoint.USEast1
                    });

                var request = new GetItemRequest
                {
                    TableName = _TableName,
                    Key = new Dictionary<string, AttributeValue>() { { "Username", new AttributeValue { S = name } } },
                };
                var dbResponse = await dynamoDbClient.GetItemAsync(request);

                // Check the response.
                var result = dbResponse.Item;

                if ((result != null) && (result.Count != 0))
                {
                    bool isInOffice = result["IsInOffice"].BOOL;

                    if (isInOffice)
                    {
                        responseSpeech = GetResponse(_isPersonHerePositiveResponses, name);
                    }
                    else
                    {
                        responseSpeech = GetResponse(_isPersonHereNegativeResponses, name);
                    }
                }
                else
                {
                    var unkknownUserResponse = await HandleUnknownUser(intentRequest, dynamoDbClient, name);
                    if (unkknownUserResponse != null)
                    {
                        return unkknownUserResponse;
                    }
                }
            }

            var response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
            {
                Text = responseSpeech
            });

            return response;
        }

        private async Task<SkillResponse> HandlePersonCameToWorkToday(SkillRequest input, ILambdaLogger logger)
        {
            IntentRequest intentRequest = input.Request as IntentRequest;
            logger.LogLine($"IntentRequest {intentRequest.Intent.Name} made");

            string responseSpeech = "Sorry, I don't know this person.";

            if (intentRequest.DialogState == DialogState.Started ||
                intentRequest.DialogState != DialogState.Completed)
            {
                // Pre-fill slots: update the intent object with slot values for which
                // you have defaults, then return Dialog.Delegate with this updated intent
                // in the updatedIntent property.
                return ResponseBuilder.DialogDelegate(input.Session);
            }
            else
            {

                if (intentRequest.Intent.Slots.TryGetValue("name", out var nameSlot))
                {
                    string name = nameSlot.Value.ToLower();

                    logger.LogLine($"Looking for person with name: {name}");

                    // Create  dynamodb client  
                    var dynamoDbClient = new AmazonDynamoDBClient(
                        new AmazonDynamoDBConfig
                        {
                            RegionEndpoint = RegionEndpoint.USEast1
                        });

                    var request = new GetItemRequest
                    {
                        TableName = _TableName,
                        Key = new Dictionary<string, AttributeValue>() { { "Username", new AttributeValue { S = name } } },
                    };
                    var dbResponse = await dynamoDbClient.GetItemAsync(request);

                    // Check the response.
                    var result = dbResponse.Item;

                    if ((result != null) && (result.Count != 0))
                    {
                        string lastTimeInOfficeString = result.ContainsKey("LastTimeInOffice") ? result["LastTimeInOffice"].S : null;

                        if (!string.IsNullOrEmpty(lastTimeInOfficeString) &&
                            lastTimeInOfficeString.Equals(DateTime.Now.ToShortDateString()))
                        {
                            responseSpeech = GetResponse(_isPersonHereTodayResponses, name);
                        }
                        else
                        {
                            responseSpeech = GetResponse(_isPersonNotHereTodayResponses, name);
                        }
                    }
                    else
                    {
                        var unkknownUserResponse = await HandleUnknownUser(intentRequest, dynamoDbClient, name);
                        if (unkknownUserResponse != null)
                        {
                            return unkknownUserResponse;
                        }
                    }
                }
            }

            var response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
            {
                Text = responseSpeech
            });

            return response;
        }

        private async Task<SkillResponse> HandleUnknownUser(IntentRequest intentRequest, AmazonDynamoDBClient dynamoDbClient, string name)
        {
            var allUsers = await dynamoDbClient.ScanAsync(new ScanRequest
            {
                TableName = _TableName,
                ProjectionExpression = "Username"
            });

            if (allUsers != null && allUsers.Count > 0)
            {
                var l = new Levenshtein();

                var bestMatchedName = allUsers.Items
                    .Select(x => x["Username"].S)
                    .OrderBy(x => l.Distance(x, name))
                    .FirstOrDefault();

                if (bestMatchedName != null)
                {
                    intentRequest.Intent.Slots.TryAdd("name", new Slot { Value = bestMatchedName });
                    intentRequest.Intent.Slots["name"].Value = bestMatchedName;
                    return ResponseBuilder.DialogConfirmIntent(new PlainTextOutputSpeech
                    {
                        Text = $"I did not find {name}, do you mean {bestMatchedName}"
                    }, intentRequest.Intent);
                }
            }

            return null;
        }

        /// <summary>
        /// Handles when the user calls the skill name
        /// </summary>
        private SkillResponse HandleLaunch(LaunchRequest launchRequest, ILambdaLogger logger)
        {
            var response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
            {
                Text = "Hello! You can ask me if someone is in the office if you want."
            });

            return response;
        }
        #endregion
    }
}
