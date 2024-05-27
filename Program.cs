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
using Spectre.Console;

internal partial class Program : ProgramBase
{
    public static async Task Main(string[] args)
    {
        var settings = ParseArguments(args);

        if (!settings.TryGetValue("filetype", out List<string>? value) || value.Count == 0)
        {
            AnsiConsole.MarkupLine("Please specify at least one filetype using [green3]-filetype \"[gold3]smtp[/],[gold3]pop3[/]\"[/].");
            AnsiConsole.MarkupLine("Valid log file types: [gold3]accounts[/],[gold3]contentfiltering[/],[gold3]imap4[/],[gold3]outmail[/],[gold3]outmailfail[/],[gold3]pop3[/],[gold3]pop3retr[/],[gold3]remoteadmin[/],[gold3]server[/],[gold3]smtp[/],[gold3]webmail[/]");
            Console.WriteLine();
            AnsiConsole.MarkupLine("To specify post processing options: use the [green3]-postprocess[/] switch followed by [green3]\"[gold3]archive[/]\"[/] or [green3]\"[gold3]delete[/]\"[/] ");
            Console.WriteLine();
            AnsiConsole.MarkupLine("If archiving, specify the path after the [green3]\"[gold3]archive[/]\"[/] option as in: ");
            AnsiConsole.MarkupLine("[green3]-postprocess \"[gold3]archive[/]\" \"[yellow3_1]C:\\MyLogArchive[/]\"[/]");
            Console.WriteLine();
            AnsiConsole.MarkupLine("If you which to perform archive or delete AFTER all files have been processed, use the following:");
            AnsiConsole.MarkupLine("[green3]-postprocess \"[gold3]archive[/]\" \"[yellow3_1]C:\\MyLogArchive[/]\" -after[/]");
            Console.WriteLine();
            AnsiConsole.MarkupLine("Example with all options specified:");
            AnsiConsole.MarkupLine("[green4]LogParserApp[/] [green3]-filetype \"[gold3]smtp[/],[gold3]pop3[/],[gold3]imap4[/]\"  -postprocess \"[gold3]archive[/]\" \"[yellow3_1]C:\\MyLogArchive[/]\" -after[/]");
            AnsiConsole.MarkupLine("This would process [gold3]smtp[/], [gold3]pop3[/] and [gold3]imap4[/] logs, archive them and do so after files parsed successfully.");
            Console.WriteLine();
            AnsiConsole.MarkupLine("The default archive folder path can be specified in the [yellow3_1]appsettings.json[/] file");
            return;
        }

        var host = CreateHostBuilder(args).Build();

        var config = host.Services.GetRequiredService<IConfiguration>();

        string? folderPath = settings.TryGetValue("folderpath", out List<string>? value1) && value1.Count > 0 ? value1[0]
                              : config["LogFileSettings:FolderPath"];

        string? archivePath = settings.TryGetValue("archivepath", out List<string>? value2) && value2.Count > 0 ? value2[0]
                              : config["LogFileSettings:ArchivePath"];

        string postProcess = settings.TryGetValue("postprocess", out List<string>? value3) && value3.Count > 0 ? value3[0].ToLower() : "keep";
        bool afterProcessing = settings.ContainsKey("after");

        List<string> processedFiles = [];

        foreach (var fileType in value)
        {
            AnsiConsole.MarkupLine($"[green3]Processing log type:[/] [yellow3_1]{fileType}[/]");

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
                var tasks = batch.Select(file => ProcessFileAsync(file, host, archivePath, postProcess, afterProcessing, processedFiles)).ToArray();
                await Task.WhenAll(tasks);
            }
        }

        if (afterProcessing)
        {
            PostProcessFiles(processedFiles, archivePath, postProcess);
        }
    }

    private static async Task<bool> ProcessFileAsync(string file, IHost host, string? archivePath, string postProcess, bool afterProcessing, List<string> processedFiles)
    {
        AnsiConsole.MarkupLine($"[green3]Processing file:[/] [yellow3_1]{file}[/]");

        using var scope = host.Services.CreateScope();
        var logFileProcessor = scope.ServiceProvider.GetRequiredService<LogFileProcessor>();

        var processSuccess = await logFileProcessor.ProcessLogFileAsync(file);

        if (processSuccess)
        {
            if (afterProcessing)
            {
                lock (processedFiles)
                {
                    processedFiles.Add(file);
                }
            }
            else
            {
                PostProcessFile(file, archivePath, postProcess);
            }
        }
        else
        {
            AnsiConsole.MarkupLine($"[red]Processing failed for file: [yellow3_1]{file}[/], skipping post-processing steps.[/]");
        }

        return processSuccess;
    }

    private static void PostProcessFile(string file, string? archivePath, string postProcess)
    {
        switch (postProcess)
        {
            case "archive":
                string targetPath = Path.Combine(archivePath ?? @"C:\logs\archive", Path.GetFileName(file));
                File.Move(file, targetPath);
                AnsiConsole.MarkupLine($"[aqua]Archived file to:[/] [yellow3_1]{targetPath}[/]");
                break;
            case "delete":
                File.Delete(file);
                AnsiConsole.MarkupLine($"[aqua]Deleted file:[/] [yellow3_1]{file}[/]");
                break;
            case "keep":
                break;
            default:
                break;
        }
    }


    private static void PostProcessFiles(List<string> files, string? archivePath, string postProcess)
    {
        foreach (var file in files)
        {
            PostProcessFile(file, archivePath, postProcess);
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
        .ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddConsole();
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });


    // generates a regex at compile time
    [GeneratedRegex(@"^.*?_(\d+)\.txt$")]
    private static partial Regex OrderKeyRegex();

}