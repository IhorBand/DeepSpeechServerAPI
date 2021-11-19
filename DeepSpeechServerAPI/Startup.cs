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

            var args = $"deepspeech --model \"{modelFullFilePath}\" --scorer \"{scorerFullFilePath}\"  --audio \"{audioFullFilePath}\" --json >> {outputFullFilePath}";

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
                                    Console.WriteLine("");
                                    Console.WriteLine($"{DateTime.UtcNow.Hour}:{DateTime.UtcNow.Minute}:{DateTime.UtcNow.Second} || File was successfullly received || Result: {eofResultStr}");
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

            FileStream fsSource = new FileStream(
                outputFullFilePath,
                FileMode.Open,
                FileAccess.Read);

            byte[] data = new byte[1024 * 8];
            while (true)
            {
                int count = fsSource.Read(data, 0, 1024 * 8);
                if (count == 0)
                {
                    break;
                }

                await webSocket.SendAsync(new ArraySegment<byte>(data), WebSocketMessageType.Binary, true, CancellationToken.None);

                var resultBuffer = new byte[1024 * 8];
                var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(resultBuffer), CancellationToken.None);
                //TODO: need to check for "OK" result
            }

            var response = new ArraySegment<byte>(Encoding.UTF8.GetBytes("{\"eof\" : 1}"));
            await webSocket.SendAsync(response, WebSocketMessageType.Text, true, CancellationToken.None);

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "OK", CancellationToken.None);

            File.Delete(path);
            File.Delete(outputFullFilePath);
        }
    }
}
