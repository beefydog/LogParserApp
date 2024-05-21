namespace LogParserApp;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using LogParserApp.Data;
using LogParserApp.Utilities;

internal partial class Program : ProgramBase
{
    public static async Task Main(string[] args)
    {
        var settings = ParseArguments(args);

        if (!settings.TryGetValue("filetype", out List<string>? value) || value.Count == 0)
        {
            Console.WriteLine("Please specify at least one filetype using '-filetype \"smtp, pop3\"'.");
            return;
        }

        var host = CreateHostBuilder(args).Build();

        // Access the configuration and the LogFileProcessor service
        var config = host.Services.GetRequiredService<IConfiguration>();

        string? folderPath = settings.TryGetValue("folderpath", out List<string>? value1) && value1.Count > 0 ? value1[0]
                              : config["LogFileSettings:FolderPath"];

        string? archivePath = settings.TryGetValue("archivepath", out List<string>? value2) && value2.Count > 0 ? value2[0]
                              : config["LogFileSettings:ArchivePath"];

        string postProcess = settings.TryGetValue("postprocess", out List<string>? value3) && value3.Count > 0 ? value3[0].ToLower() : "keep";


        foreach (var fileType in value)
        {
            var logFiles = Directory.GetFiles(folderPath ?? @"C:\logs", $"{fileType}_*.txt")
                .Select(file => new
                {
                    FileName = file,
                    OrderKey = int.Parse(OrderKeyRegex().Match(Path.GetFileName(file)).Groups[1].Value)
                })
                .OrderBy(f => f.OrderKey)
                .Select(f => f.FileName);

            var logFileBatches = logFiles.ChunkBy(5); // Process files in batches of 5

            foreach (var batch in logFileBatches)
            {
                var tasks = batch.Select(file => ProcessFileAsync(file, host, archivePath, postProcess)).ToArray();
                await Task.WhenAll(tasks);
            }
        }
    }

    private static async Task ProcessFileAsync(string file, IHost host, string? archivePath, string postProcess)
    {
        Console.WriteLine($"Processing file: {file}");

        using var scope = host.Services.CreateScope();
        var logFileProcessor = scope.ServiceProvider.GetRequiredService<LogFileProcessor>();

        var processSuccess = await logFileProcessor.ProcessLogFileAsync(file);

        if (processSuccess)
        {
            switch (postProcess)
            {
                case "archive":
                    string targetPath = Path.Combine(archivePath ?? @"C:\logs\archive", Path.GetFileName(file));
                    File.Move(file, targetPath);
                    Console.WriteLine($"Archived file to: {targetPath}");
                    break;
                case "delete":
                    File.Delete(file);
                    Console.WriteLine($"Deleted file: {file}");
                    break;
                case "keep":
                    // Nothing to do, may add something later to keep, but rename, or what-have-you
                    break;
            }
        }
        else
        {
            Console.WriteLine($"Processing failed for file: {file}, skipping post-processing steps.");
        }
    }

    static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureAppConfiguration((hostingContext, config) =>
        {
            config.SetBasePath(Directory.GetCurrentDirectory());
            config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        })
        .ConfigureServices((hostContext, services) =>
        {
            services.AddDbContext<LogDbContext>(options =>
                options.UseSqlServer(hostContext.Configuration.GetConnectionString("DefaultConnection")));

            services.AddScoped<LogFileProcessor>();
            services.AddLogging(); 

            services.AddSingleton<IConfiguration>(hostContext.Configuration);
        })
        .ConfigureLogging(logging => {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });


    // generates a regex at compile time
    [GeneratedRegex(@"^.*?_(\d+)\.txt$")]
    private static partial Regex OrderKeyRegex();

}