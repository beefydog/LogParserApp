using System;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using EFCore.BulkExtensions;
using LogParserApp.Entities;
using LogParserApp.Utilities;
using static LogParserApp.Utilities.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using LogParserApp.Data;


namespace LogParserApp
{
    public partial class LogFileProcessor(LogDbContext dbContext, ILogger<LogFileProcessor> logger)
    {
        private readonly LogDbContext _dbContext = dbContext;
        private readonly ILogger<LogFileProcessor> _logger = logger;

        public async Task<bool> ProcessLogFileAsync(string filePath)
        {
            bool result = true;

            List<LogEntry> logEntries = [];
            string[] lines = [];
            string fileName = string.Empty;
            string fileNameNoExt;
            string? folderName = string.Empty;
            DateTime fileDate = new();
            string fileHash = string.Empty;
            int logFileId = 0;
            string fileType = string.Empty;
            List<ParsingError> errors = [];

            try
            {
                fileName = Path.GetFileName(filePath);
                fileNameNoExt = Path.GetFileNameWithoutExtension(filePath);
                folderName = Path.GetDirectoryName(filePath);
                fileDate = File.GetLastWriteTime(filePath);
                fileHash = await Hashing.ComputeSha256HashAsync(filePath);
                logFileId = ExtractLogFileId(fileNameNoExt);
                fileType = ExtractFileType(fileNameNoExt);
                lines = await File.ReadAllLinesAsync(filePath);
            }
            catch (FileNotFoundException ex)
            {
                _logger.LogError("File not found: {ex.FileName} {ex.Message}", ex.FileName, ex.Message);
                errors.Add(new ParsingError { FileName = fileName ?? "", ErrorMessage = ex.Message.FormatExceptionMessageForDb() });
                result = false;
            }
            catch (DirectoryNotFoundException ex)
            {
                _logger.LogError("Directory not found: {folderName} {ex.Message}", folderName, ex.Message);
                errors.Add(new ParsingError { FileName = fileName ?? "", ErrorMessage = ex.Message.FormatExceptionMessageForDb() });
                result = false;
            }
            catch (SecurityException ex)
            {
                _logger.LogError("Access denied: {filePath} {ex.Message}", filePath, ex.Message);
                errors.Add(new ParsingError { FileName = fileName ?? "", ErrorMessage = ex.Message.FormatExceptionMessageForDb() });
                result = false;
            }
            catch (IOException ex)
            {
                _logger.LogError("I/O Error: {filePath} {ex.Message}", filePath, ex.Message);
                errors.Add(new ParsingError { FileName = fileName ?? "", ErrorMessage = ex.Message.FormatExceptionMessageForDb() });
                result = false;
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogError("Access denied: {filePath} {ex.Message}", filePath, ex.Message);
                errors.Add(new ParsingError { FileName = fileName ?? "", ErrorMessage = ex.Message.FormatExceptionMessageForDb() });
                result = false;
            }

            if (await LogAlreadyProcessedAsync(fileName))
            {
                _logger.LogInformation("Log file already processed: {fileName}", fileName);
                result = false;
            }

            if (!result) return false;

            using var transaction = await _dbContext.Database.BeginTransactionAsync();
            try
            {
                var parsedLog = new ParsedLog
                {
                    FileName = fileName!,
                    LogType = fileType,
                    LogFileId = logFileId,
                    DateParsed = DateTime.UtcNow,
                    FileDate = fileDate,
                    FileHash = fileHash
                };

                await _dbContext.ParsedLogs.AddAsync(parsedLog);
                await _dbContext.SaveChangesAsync();

                int parsedLogId = parsedLog.Id;

                int lineNum = 0;
                foreach (var line in lines)
                {
                    var entry = ParseLine(line, parsedLogId, lineNum);
                    if (entry != null)
                    {
                        logEntries.Add(entry);
                    }
                    else
                    {
                        errors.Add(new ParsingError { FileName = fileName ?? "", ErrorMessage = $"Unable to parse or convert line {lineNum}" });
                    }
                    lineNum += 1;
                }

                await _dbContext.BulkInsertAsync(logEntries);
                await _dbContext.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Log file: {fileName} processed and data committed to the database.", fileName);
                result = true;
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError("Error processing log file: {fileName} {ex.Message}", fileName, ex.Message);
                errors.Add(new ParsingError { FileName = fileName ?? "", ErrorMessage = ex.Message.FormatExceptionMessageForDb() });
                result = false;
            }
            if (!result || errors.Count > 1)
            {
                await _dbContext.ParsingErrors.AddRangeAsync(errors);
                await _dbContext.SaveChangesAsync();
            };
            return result;
        }


        private async Task<bool> LogAlreadyProcessedAsync(string? fileName)
        {
            return fileName != null && await _dbContext.ParsedLogs.AsNoTracking().AnyAsync(l => l.FileName == fileName);
        }

        private static string ExtractFileType(string fileNameNoExt)
        {
            var match = FileTypeRegex().Match(fileNameNoExt);
            return match.Success ? match.Groups[1].Value : "unknown";
        }

        private static int ExtractLogFileId(string fileNameNoExt)
        {
            var match = FileIdRegex().Match(fileNameNoExt);
            return match.Success ? int.Parse(match.Groups[1].Value) : 0;
        }

        private static LogEntry? ParseLine(string line, int parsedLogId, int lineNum)
        {
            try
            {
                var parts = line.RemoveNullChars().Split("->", StringSplitOptions.TrimEntries); //null characters "\0" cause problems
                if (parts.Length < 2) return null;

                var dateTimePart = parts[0].Trim();
                string ipPart;
                string statusAndRestPart;

                // Check if the IP address field present (some logs do not include IP address)
                if (parts.Length == 3)
                {
                    ipPart = parts[1].Trim();
                    statusAndRestPart = parts[2].Trim();
                }
                else
                {
                    ipPart = string.Empty;
                    statusAndRestPart = parts[1].Trim();
                }

                var statusPart = statusAndRestPart.Split(':', StringSplitOptions.TrimEntries)[0];
                var actionDetailsPart = ActionDetailsRegex().Match(statusAndRestPart);

                string action = actionDetailsPart.Groups[1].Value.Trim();
                string details = actionDetailsPart.Groups.Count > 2 ? actionDetailsPart.Groups[2].Value.Trim() : string.Empty;

                return new LogEntry
                {
                    ParsedLogId = parsedLogId,
                    LineNum = lineNum,
                    EntryDate = DateTime.ParseExact(dateTimePart, "ddd, dd MMM yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                    IPaddress = ipPart,
                    Status = statusPart,
                    Action = action,
                    Details = details
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Error processing line# {lineNum} {ex.Message}");
            }
        }

        // generates all regexes at compile time
        [GeneratedRegex(@"^(.*?)_\d+$")]
        private static partial Regex FileTypeRegex();

        [GeneratedRegex(@"_([0-9]+)$")]
        private static partial Regex FileIdRegex();

        [GeneratedRegex(@"Action=\[(.*?)\](?:, Details=\[(.*?)\])?", RegexOptions.Compiled)]
        private static partial Regex ActionDetailsRegex();
    }
}