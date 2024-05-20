using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LogParserApp.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;


namespace LogParserApp.Data;

public class LogDbContext(DbContextOptions<LogDbContext> options) : DbContext(options)
{
    public DbSet<LogEntry> LogEntries { get; set; }
    public DbSet<ParsedLog> ParsedLogs { get; set; }
    public DbSet<ParsingError> ParsingErrors { get; set; }
}

