using Alexa.NET;
using Alexa.NET.Request;
using Alexa.NET.Request.Type;
using Alexa.NET.Response;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AccediaLocator.PersoIsInOffice
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
                case IntentRequest intentRequest: return await HandleIntent(intentRequest, logger);
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
        }
        #endregion

        #region Responses pool
        private List<string> _isPersonHerePositiveResponses;
        private List<string> _isPersonHereNegativeResponses;
        private List<string> _whereIsPersonResponses;

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
            responses.Add("No, {0} is not here yet.");
            responses.Add("No, {0} has not arrived at the office.");
            responses.Add("This {0} person, you are looking for is not here.");
            responses.Add("{0}, is not here.");
            responses.Add("{0} is not in the office.");
            responses.Add("Nope, {0} did not come to work today.");

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

        private string GetPersonIsHerePositiveResponse(string name)
        {
            Random rand = new Random();
            int index = rand.Next(0, _isPersonHerePositiveResponses.Count);

            return string.Format(this._isPersonHerePositiveResponses[index], name);
        }

        private string GetPersonIsHereNegativeResponse(string name)
        {
            Random rand = new Random();
            int index = rand.Next(0, _isPersonHereNegativeResponses.Count);

            return string.Format(this._isPersonHereNegativeResponses[index], name);
        }

        private string GetWhereIsPersonResponse(string name, string room)
        {
            Random rand = new Random();
            int index = rand.Next(0, _whereIsPersonResponses.Count);

            return string.Format(this._whereIsPersonResponses[index], name, room);
        }
        #endregion

        #region Handle alexa requests
        /// <summary>
        /// Handles the intent and returns the required information
        /// </summary>
        private async Task<SkillResponse> HandleIntent(IntentRequest intentRequest, ILambdaLogger logger)
        {
            if (intentRequest.Intent.Name == "PersonIsInOfficeIntent")
            {
                return await HandleIsPersonInOfficeIntent(intentRequest, logger);
            }
            else if(intentRequest.Intent.Name == "WhereIsPersonIntent")
            {
                return await HandlewhereIsPersonIntent(intentRequest, logger);
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
                // Create  dynamodb client  
                var dynamoDbClient = new AmazonDynamoDBClient(
                    new AmazonDynamoDBConfig
                    {
                        RegionEndpoint = RegionEndpoint.USEast1
                    });

                var request = new GetItemRequest
                {
                    TableName = _TableName,
                    Key = new Dictionary<string, AttributeValue>() { { "Username", new AttributeValue { S = name} } },
                };
                var dbResponse = await dynamoDbClient.GetItemAsync(request);
                
                // Check the response.
                var result = dbResponse.Item;

                if ((result != null) && (result.Count != 0))
                {
                    string room = result["Room"].S;
                    bool isInOffice = result["IsInOffice"].BOOL;

                    if (isInOffice)
                    {
                        responseSpeech = GetWhereIsPersonResponse(name, room);
                    }
                    else
                    {
                        responseSpeech = $"{name} has not arrived at the office yet.";
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
                // Create  dynamodb client  
                var dynamoDbClient = new AmazonDynamoDBClient(
                    new AmazonDynamoDBConfig
                    {
                        RegionEndpoint = RegionEndpoint.USEast1
                    });

                var request = new GetItemRequest
                {
                    TableName = _TableName,
                    Key = new Dictionary<string, AttributeValue>() { { "Username", new AttributeValue { S = name} } },
                };
                var dbResponse = await dynamoDbClient.GetItemAsync(request);

                // Check the response.
                var result = dbResponse.Item;

                if ((result != null) && (result.Count != 0))
                {
                    bool isInOffice = result["IsInOffice"].BOOL;

                    if (isInOffice)
                    {
                        responseSpeech = GetPersonIsHerePositiveResponse(name);
                    }
                    else
                    {
                        responseSpeech = GetPersonIsHereNegativeResponse(name);
                    }
                }
            }

            var response = ResponseBuilder.Tell(new PlainTextOutputSpeech()
            {
                Text = responseSpeech
            });

            return response;
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
