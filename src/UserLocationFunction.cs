using AccediaLocator.Models;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AccediaLocator
{
    public class UserLocationFunction
    {
        private readonly string _tableName = "accedia-locator-users";

        //Function Handler is an entry point to start execution of Lambda Function.  
        //It takes Input Data as First Parameter and ObjectContext as Second  
        public async Task FunctionHandler(UserLocationModel locationModel, ILambdaContext context)
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

            // Update location of user
            LambdaLogger.Log("Update location for record");

            locationModel.Username = locationModel.Username.ToLowerInvariant();
            locationModel.Room = locationModel.Room.ToLowerInvariant();

            if (!locationModel.IsInOffice)
            {
                locationModel.Room = "out";
            }
            else if (string.IsNullOrWhiteSpace(locationModel.Room))
            {
                throw new ArgumentNullException("room");
            }

            if (locationModel.Room == "somewhere")
            {
                await dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "Username", new AttributeValue(locationModel.Username) }
                    },
                    TableName = _tableName,
                    UpdateExpression = "SET IsInOffice = :o, Room = :r",
                    ConditionExpression = "Room <> :r",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":o", new AttributeValue{ BOOL = locationModel.IsInOffice} },
                        { ":r", new AttributeValue { S = locationModel.Room } }
                    }
                });
            }
            else
            {
                await dynamoDbClient.UpdateItemAsync(new UpdateItemRequest
                {
                    Key = new Dictionary<string, AttributeValue>
                    {
                        { "Username", new AttributeValue(locationModel.Username) }
                    },
                    TableName = _tableName,
                    UpdateExpression = "SET IsInOffice = :o, Room = :r, RoomHistory = list_append(if_not_exists(RoomHistory, :empty_list), :room_history_record)",
                    ConditionExpression = "Room <> :r",
                    ExpressionAttributeValues = new Dictionary<string, AttributeValue>
                    {
                        { ":o", new AttributeValue{ BOOL = locationModel.IsInOffice} },
                        { ":r", new AttributeValue { S = locationModel.Room } },
                        { ":room_history_record", new AttributeValue { L = new List<AttributeValue>{ new AttributeValue { S = locationModel.Room } } } },
                        { ":empty_list", new AttributeValue { IsLSet = true } },
                    }
                });
            }

            //Write Log to cloud watch using context.Logger.Log Method  
            context.Logger.Log(string.Format("Finished execution for function -- {0} at {1}",
                               context.FunctionName, DateTime.Now));
        }
    }
}