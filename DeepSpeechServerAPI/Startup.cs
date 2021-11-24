using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace DeepSpeechServerAPI
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo { Title = "DeepSpeechServerAPI", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "DeepSpeechServerAPI v1"));
            }

            app.UseHttpsRedirection();
                        
            app.UseRouting();

            //app.UseAuthorization();

            var webSocketOptions = new WebSocketOptions()
            {
                KeepAliveInterval = TimeSpan.FromSeconds(120)
            };

            app.UseWebSockets(webSocketOptions);

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
            });

            app.Use(async (context, next) =>
            {
                if (context.Request.Path == "/ws")
                {
                    if (context.WebSockets.IsWebSocketRequest)
                    {
                        WebSocket webSocket = await context.WebSockets.AcceptWebSocketAsync();
                        await Echo(context, webSocket);
                    }
                    else
                    {
                        context.Response.StatusCode = 400;
                    }
                }
                else
                {
                    await next();
                }
            });
        }

        private async Task Echo(HttpContext context, WebSocket webSocket)
        {
            //TODO: need to pass these parameters from command line

            var path = "/input/input.wav";
            //var path = "B:\\tmp.wav";

            var modelDirPath = "/src/Models/en";

            var modelFileName = "deepspeech-0.9.3-models.pbmm";
            var scorerFileName = "deepspeech-0.9.3-models.scorer";

            var modelFullFilePath = $"{modelDirPath}/{modelFileName}";
            var scorerFullFilePath = $"{modelDirPath}/{scorerFileName}";

            var audioFullFilePath = $"/src/Test/websocket_test.wav";
            audioFullFilePath = path;

            var outputFullFilePath = $"/output/output.js";
            var outputTempFullFilePath = $"/output/output_temp.js";

            var args = $"deepspeech --model \"{modelFullFilePath}\" --scorer \"{scorerFullFilePath}\"  --audio \"{audioFullFilePath}\" --json >> {outputTempFullFilePath}";

            Console.WriteLine("");
            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Receiving Audio File From Client...");

            {
                var buffer = new byte[1024 * 8];
                WebSocketReceiveResult result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                while (!result.CloseStatus.HasValue)
                {
                    if (result.MessageType == WebSocketMessageType.Text)
                    {
                        using (var ms = new MemoryStream())
                        {
                            do
                            {
                                var eofResultBuffer = new ArraySegment<byte>(buffer);
                                ms.Write(eofResultBuffer.Array, eofResultBuffer.Offset, result.Count);
                            }
                            while (!result.EndOfMessage);

                            ms.Seek(0, SeekOrigin.Begin);

                            using (var reader = new StreamReader(ms, Encoding.UTF8))
                            {
                                var eofResultStr = reader.ReadToEnd();
                                if (eofResultStr.Contains("{\"eof\" : 1}"))
                                {
                                    Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Audio File was successfullly received || Result: {eofResultStr}");
                                    break;
                                }
                            }
                        }
                    }
                    else
                    {
                        using (var stream = new FileStream(path, File.Exists(path) ? FileMode.Append : FileMode.OpenOrCreate))
                        {
                            stream.Write(buffer, 0, buffer.Length);
                        }

                        await webSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes("OK")), WebSocketMessageType.Text, true, CancellationToken.None);
                        result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
                    }
                }
            }

            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Transcribing Audio...");

            string error = string.Empty;
            string output = string.Empty;

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "/bin/bash",
                Arguments = $"-c \"{args}\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            startInfo.ErrorDialog = false;

            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardInput = true;

            var process = System.Diagnostics.Process.Start(startInfo);

            using (System.IO.StreamReader myError = process.StandardError)
            {
                error = myError.ReadToEnd();
            }

            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Audio was successfullly Transcribed.");
            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Parsing JSON...");

            // we can grab only first array of words, because it's always the most confident transcript
            JsonSerializer serializer = new JsonSerializer();
            JsonModels.WordInfo wordInfo = null;
            using (FileStream fs = File.Open(outputTempFullFilePath, FileMode.Open))
            using (StreamReader sr = new StreamReader(fs))
            using (JsonReader reader = new JsonTextReader(sr))
            {
                // Advance the reader to start of array(words array)
                while (reader.Path != "transcripts[0].words")
                {
                    reader.Read();
                }

                while (reader.Read())
                {
                    // if we done with first array -> break cycle and send text to the client
                    if(reader.Path == "transcripts[0]" || reader.Path == "transcripts[1]")
                    {
                        break;
                    }

                    // deserialize only when there's "{" character in the stream
                    if (reader.TokenType == JsonToken.StartObject)
                    {
                        try
                        {
                            wordInfo = serializer.Deserialize<JsonModels.WordInfo>(reader);
                            if (wordInfo != null)
                            {
                                File.AppendAllText(outputFullFilePath, wordInfo.Word + " ");
                            }
                        }
                        catch(Exception ex)
                        {
                            //TOOD: log with log4net
                            Console.WriteLine($"Error: {ex.Message} \n StackTrace: {ex.StackTrace}");
                        }
                    }
                }

                fs.Close();
            }

            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || JSON was successfullly Parsed.");
            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Sending Result to the Client...");

            FileStream fsSource = new FileStream(
                outputFullFilePath,
                FileMode.Open,
                FileAccess.Read);

            int bytesToRead = 1024 * 8;
            byte[] data = new byte[bytesToRead];
            while (true)
            {
                int count = fsSource.Read(data, 0, data.Length);
                if (count <= 0)
                {
                    break;
                }

                await webSocket.SendAsync(new ArraySegment<byte>(data, 0, count), WebSocketMessageType.Binary, true, CancellationToken.None);

                var resultBuffer = new byte[bytesToRead];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(resultBuffer), CancellationToken.None);
                //TODO: need to check for "OK" result
            }

            fsSource.Close();

            var response = new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"eof\" : 1}"));
            await webSocket.SendAsync(response, WebSocketMessageType.Text, true, CancellationToken.None);

            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Result was successfully sent to the client.");
            Console.WriteLine($"{DateTime.UtcNow.Date.Year}-{DateTime.UtcNow.Date.Month}-{DateTime.UtcNow.Day} {DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || INFO || Closing connection with client.");

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);

            File.Delete(path);
            File.Delete(outputFullFilePath);
            File.Delete(outputTempFullFilePath);
        }
    }
}
