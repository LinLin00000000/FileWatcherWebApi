using System.Net;
using TinyHotKey;

namespace FileWatcherWebApi
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseStartup<Startup>();
                    // webBuilder.UseUrls("http://localhost:7544");
                    
                    webBuilder.ConfigureKestrel((context, options) =>
                    {
                        var loggerFactory = LoggerFactory.Create(builder =>
                        {
                            builder.AddConsole();
                        });

                        var logger = loggerFactory.CreateLogger<Program>();

                        var settings = context.Configuration.GetSection("ApplicationSettings");
                        // bool hasError = false;

                        // if (!Enum.TryParse<Key>(settings["ToggleKey"], true, out var toggleKey))
                        // {
                        //     logger.LogError("Invalid configuration for TriggerKey");
                        //     hasError = true;
                        // }

                        // if (!Enum.TryParse<Key>(settings["TriggerKey"], true, out var triggerKey))
                        // {
                        //     logger.LogError("Invalid configuration for TriggerKey");
                        //     hasError = true;
                        // }

                        // var targetWindowTitle = settings["TargetWindowTitle"];
                        // if (string.IsNullOrEmpty(targetWindowTitle))
                        // {
                        //     logger.LogError("TargetWindowTitle configuration is missing");
                        //     hasError = true;
                        // }

                        // if (!int.TryParse(settings["Interval_MS"], out var interval))
                        // {
                        //     logger.LogError("Invalid configuration for Interval_MS");
                        //     hasError = true;
                        // }

                        // if (!int.TryParse(settings["Jitter_MS"], out var jitter))
                        // {
                        //     logger.LogError("Invalid configuration for Jitter_MS");
                        //     hasError = true;
                        // }

                        // if (!IPAddress.TryParse(settings["Host"], out var host))
                        // {
                        //     logger.LogError("Invalid configuration for Host");
                        //     hasError = true;
                        // }

                        // if (!int.TryParse(settings["Port"], out var port))
                        // {
                        //     logger.LogError("Invalid configuration for Port");
                        //     hasError = true;
                        // }

                        // if (hasError)
                        // {
                        //     logger.LogError("Please check your appsettings.json file and ensure the settings are correct.");
                        // }
                        // else if (host != null)
                        // {
                        //     options.Listen(host, port);
                        //     // KeyPressScheduler scheduler = new(toggleKey, triggerKey, targetWindowTitle, interval, jitter);
                        //     // scheduler.Start();
                        //     // KeyPressScheduler.Test();
                        // }

                        if (!string.IsNullOrEmpty(settings["Host"])
                            && IPAddress.TryParse(settings["Host"], out var host)
                            && int.TryParse(settings["Port"], out var port))
                        {
                            options.Listen(host, port);
                        }
                        else
                        {
                            options.Listen(IPAddress.Parse("127.0.0.1"), 7543);
                        }
                    });
                });
    }

    public class FileWatcherService
    {
        private readonly ILogger<FileWatcherService> _logger;
        private readonly FileSystemWatcher _watcher = new();
        private readonly List<StreamWriter> _clients = [];
        private readonly object _clientsLock = new();

        public FileWatcherService(ILogger<FileWatcherService> logger)
        {
            _logger = logger;
            ConfigureWatcher();
        }

        private void ConfigureWatcher()
        {
            var watchFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                "Escape from Tarkov",
                "Screenshots"
            );

            if (!Directory.Exists(watchFolder))
            {
                Directory.CreateDirectory(watchFolder);
            }

            _watcher.Path = watchFolder;
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;

            _watcher.Created += async (sender, e) =>
            {
                try
                {
                    if (!string.IsNullOrEmpty(e.Name))
                    {
                        _logger.LogInformation($"New file detected: {e.FullPath}");

                        await SendFileNameToClientsAsync(e.Name);

                        await Task.Delay(TimeSpan.FromSeconds(3));
                        File.Delete(e.FullPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception in file watcher task");
                }
            };

            _watcher.EnableRaisingEvents = true;
        }

        private async Task SendFileNameToClientsAsync(string fileName)
        {
            List<StreamWriter> clientsSnapshot;
            lock (_clientsLock)
            {
                clientsSnapshot = new List<StreamWriter>(_clients);
            }

            foreach (var client in clientsSnapshot)
            {
                try
                {
                    await client.WriteAsync($"data: {fileName}\n\n");
                    await client.FlushAsync();
                }
                catch
                {
                    lock (_clientsLock)
                    {
                        _clients.Remove(client);
                    }
                }
            }
        }

        public void AddClient(StreamWriter client)
        {
            lock (_clientsLock)
            {
                _clients.Add(client);
            }
        }

        public void RemoveClient(StreamWriter client)
        {
            lock (_clientsLock)
            {
                _clients.Remove(client);
            }
        }
    }

    public class Startup
    {
        public IConfiguration Configuration { get; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddCors(options =>
            {
                options.AddPolicy("AllowAllOrigins",
                    builder => builder.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            });

            services.AddSingleton<FileWatcherService>();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, FileWatcherService fileWatcherService)
        {
            app.UseCors("AllowAllOrigins");
            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/map", async context =>
                {
                    context.Response.Headers.ContentType = "text/event-stream";
                    var responseWriter = new StreamWriter(context.Response.Body);

                    fileWatcherService.AddClient(responseWriter);

                    try
                    {
                        await Task.Delay(Timeout.Infinite, context.RequestAborted);
                    }
                    catch (TaskCanceledException)
                    {
                        // 客户端断开连接
                    }
                    finally
                    {
                        fileWatcherService.RemoveClient(responseWriter);
                    }
                });
            });
        }
    }
}
