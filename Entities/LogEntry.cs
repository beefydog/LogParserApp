using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LogParserApp.Entities;
public class LogEntry
{
    public long Id { get; set; }
    public int ParsedLogId { get; set; }
    public int LineNum { get; set; }
    public DateTime EntryDate { get; set; }
    public string IPaddress { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

}