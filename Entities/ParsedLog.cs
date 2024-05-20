using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParserApp.Entities;
public class ParsedLog
{
    public int Id { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string LogType { get; set; } = string.Empty;
    public int LogFileId { get; set; }
    public DateTime DateParsed { get; set; }
    public DateTime FileDate { get; set; }
    public string? FileHash { get; set; }  // SHA-256 hash of the file
}
