using AccediaLocator.Models;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.Json.JsonSerializer))]

namespace AccediaLocator
{
    public class UserLoginFunction
    {
        private readonly string _tableName = "accedia-locator-users";

        //Function Handler is an entry point to start execution of Lambda Function.  
        //It takes Input Data as First Parameter and ObjectContext as Second  
        public async Task FunctionHandler(UserLoginModel loginModel, ILambdaContext context)
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

            //Create Table if it Does Not Exists  
            await CreateTable(dynamoDbClient, _tableName);

            // Insert record in dynamodbtable  
            LambdaLogger.Log("Insert record in the table");
            await dynamoDbClient.PutItemAsync(new PutItemRequest
            {
                ConditionExpression = "attribute_not_exists(Username)",
                TableName = _tableName,
                Item = new Dictionary<string, AttributeValue>
                {
                    { "Username", new AttributeValue(loginModel.UserName.ToLowerInvariant()) },
                    { "Fullname", new AttributeValue{ S = loginModel.FullName} },
                    { "IsInOffice", new AttributeValue{ BOOL = false} },
                    { "Room", new AttributeValue{ S = "Out" } }
                 }
            });

            //Write Log to cloud watch using context.Logger.Log Method  
            context.Logger.Log(string.Format("Finished execution for function -- {0} at {1}",
                               context.FunctionName, DateTime.Now));
        }

        //Create Table if it does not exists  
        private async Task CreateTable(IAmazonDynamoDB amazonDynamoDBclient, string tableName)
        {
            //Write Log to Cloud Watch using LambdaLogger.Log Method  
            LambdaLogger.Log(string.Format("Creating {0} Table", tableName));

            var tableCollection = await amazonDynamoDBclient.ListTablesAsync();

            if (!tableCollection.TableNames.Contains(tableName))
                await amazonDynamoDBclient.CreateTableAsync(new CreateTableRequest
                {
                    TableName = tableName,
                    KeySchema = new List<KeySchemaElement> {
                      { new KeySchemaElement { AttributeName="Username",  KeyType= KeyType.HASH }}
                  },
                    AttributeDefinitions = new List<AttributeDefinition> {
                      new AttributeDefinition { AttributeName="Username", AttributeType="S" }
               },
                    ProvisionedThroughput = new ProvisionedThroughput
                    {
                        ReadCapacityUnits = 5,
                        WriteCapacityUnits = 5
                    },
                });
        }
    }
}