using AccediaLocator.Models;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AccediaLocator
{
    public class UserFavoriteLocationFunction
    {
        private readonly string _tableName = "accedia-locator-users";

        //Function Handler is an entry point to start execution of Lambda Function.  
        //It takes Input Data as First Parameter and ObjectContext as Second  
        public async Task<object> FunctionHandler(UserLocationModel model, ILambdaContext context)
        {
            //Write Log to Cloud Watch using Console.WriteLline.    
            Console.WriteLine("Execution started for function -  {0} at {1}",
                                context.FunctionName, DateTime.Now);

            // Create  dynamodb client  
            var dynamoDbClient = new AmazonDynamoDBClient(
                new AmazonDynamoDBConfig
                {
                    //ServiceURL = _serviceUrl,
                    RegionEndpoint = RegionEndpoint.USEast1,

                });

            // Find favorite location for user
            LambdaLogger.Log("Find favorite location");

            var locationChanges = await dynamoDbClient.GetItemAsync(new GetItemRequest
            {
                Key = new Dictionary<string, AttributeValue>
                {
                    { "Username", new AttributeValue(model.Username.ToLowerInvariant()) }
                },
                TableName = _tableName,
                ProjectionExpression = "RoomHistory"
            });

            string favoriteRoom = null;
            if (locationChanges.IsItemSet && locationChanges.Item.ContainsKey("RoomHistory"))
            {
                List<string> rooms = locationChanges.Item["RoomHistory"]
                    .L
                    .Select(x => x.S)
                    .ToList();
                favoriteRoom = FindFavoriteRoom(rooms);
            }

            //Write Log to cloud watch using context.Logger.Log Method  
            context.Logger.Log(string.Format("Finished execution for function -- {0} at {1}",
                               context.FunctionName, DateTime.Now));

            return new { FavoriteRoom = favoriteRoom };
        }

        private string FindFavoriteRoom(List<string> rooms)
        {
            var frequencies = new Dictionary<string, int>();
            string favoriteRoom = null;
            int highestFreq = 0;

            foreach (string room in rooms)
            {
                string r = room.ToLowerInvariant();
                if(r == "out")
                {
                    continue;
                }

                int freq;
                frequencies.TryGetValue(r, out freq);
                freq += 1;

                if (freq > highestFreq)
                {
                    highestFreq = freq;
                    favoriteRoom = r;
                }

                frequencies[r] = freq;
            }

            return favoriteRoom;
        }
    }
}