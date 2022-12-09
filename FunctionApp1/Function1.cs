using Azure;
using Azure.Data.Tables;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using System;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace FunctionApp1
{
    public class ShortRequest
    {
        public string ShortCode { get; set; }

        public string Input { get; set; }
    }

    public class ShortUrl : ITableEntity
    {
        public string Url { get; set; }

        public string RowKey { get; set; } = default!;

        public string PartitionKey { get; set; } = default!;

        public ETag ETag { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; } = default!;
    }
    public class NextId : ITableEntity
    {
        public int Id { get; set; }

        public string RowKey { get; set; } = default!;

        public string PartitionKey { get; set; } = default!;


        public ETag ETag { get; set; } = default!;

        public DateTimeOffset? Timestamp { get; set; } = default!;
    }
    public class Function1
    {
        [FunctionName("ShortenUrl")]
        public async Task<IActionResult> ShortenUrl(
       [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestMessage req,
       [Table("urls1", "1", "KEY", Take = 1)] NextId keyTable)
        {
            if (req == null)
            {
                return new NotFoundResult();
            }


            ShortRequest input = await req.Content.ReadAsAsync<ShortRequest>();

            if (input == null)
            {
                return new NotFoundResult();
            }

            try
            {
                TableServiceClient tableServiceClient = new TableServiceClient(Environment.GetEnvironmentVariable("AzureWebJobsStorage"));

                TableClient tableOut = tableServiceClient.GetTableClient(
                    tableName: "urls1"
                    );

                await tableOut.CreateIfNotExistsAsync();

                if (keyTable == null)
                {
                    keyTable = new NextId
                    {
                        PartitionKey = "1",
                        RowKey = "KEY",
                        Id = 1024
                    };
                    await tableOut.AddEntityAsync<NextId>(keyTable);
                }

                // strategy for getting a new code 
                string code = string.Empty;

                string Alphabet = "abcdefghijklmnopqrstuvwxyz0123456789";
                int i = keyTable.Id++;
                if (string.IsNullOrEmpty(input.ShortCode))
                {
                    if (i == 0)
                        code = Alphabet[0].ToString();
                    var s = string.Empty;
                    while (i > 0)
                    {
                        s += Alphabet[i % Alphabet.Length];
                        i = i / Alphabet.Length;
                    }

                    code = string.Join(string.Empty, s.Reverse());
                }
                else
                {
                    code = string.Join(string.Empty, input.ShortCode);
                }


                await tableOut.UpdateEntityAsync(keyTable, Azure.ETag.All, TableUpdateMode.Replace);

                var newUrl = new ShortUrl
                {
                    PartitionKey = $"{code.First()}",
                    RowKey = code,
                    Url = input.Input
                };

                var operationResult = await tableOut.AddEntityAsync(newUrl);

                return new OkObjectResult("done");
            }
            catch (Exception ex)
            {
                return new BadRequestObjectResult(ex);
            }
        }

    }
}
