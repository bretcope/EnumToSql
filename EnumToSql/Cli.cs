using System;
using System.Collections.Generic;
using System.IO;
using EnumToSql.Logging;

namespace EnumToSql
{
    /// <summary>
    /// Provides a command line interface to run EnumsToSql.
    /// </summary>
    public static class Cli
    {
        const string ASM = "--asm";
        const string CONN = "--conn";
        const string DB = "--db";
        const string SERVER = "--server";
        const string ATTR = "--attr";
        const string FORMAT = "--format";
        const string DELIMITER = "--delimiter";
        const string NO_PARALLEL = "--no-parallel";
        const string PREVIEW = "--preview";
        const string HELP = "--help";

        static readonly string s_helpMessage = $@"Replicates enums in the selected assemblies to SQL Server.

  You must provide the {ASM} argument, and at least one of: {CONN}, {DB} or
  {PREVIEW}.

  See https://github.com/bretcope/EnumsToSql for a full description and
  documentation.

OPTIONS

  {ASM} <value,...>     A comma-delimited list of .NET assemblies to load and
                        search for enums in. These can be DLLs or EXEs.

  {CONN} <value,...>    A comma-delimited list of connection strings to SQL
                        Server databases. These databases will be updated based
                        on the enums found in the {ASM} assemblies. Cannot be
                        used with the {DB} argument.

  {DB} <value,...>      For use with integrated auth. A comma-delimited list of
                        SQL Server databases. These databases will be updated
                        based on the enums found in the {ASM} assemblies. Either
                        specify the server using the {SERVER} argument, or
                        localhost is assumed. All databases must reside on the
                        same server. If multiple servers are required, use the
                        {CONN} argument. {DB} cannot be used with {CONN}.

  {SERVER} <value>      For use with integrated auth. Specifies the server where
                        the SQL Server databases listed with {DB} exist.
                        Defaults to localhost.

  {ATTR} <value>        The name of the attribute which marks enums for
                        replication to SQL Server. Defaults to ""{EnumToSqlReplicator.DEFAULT_ATTRIBUTE_NAME}"".

  {FORMAT} <value>      Sets the output format. Possible values:

                            ""plain"":       (default) Nested blocks are indented.

                            ""timestamps"":  Messages have timestamps. Nested
                                             blocks are indented.

                            ""colors"":      Nested blocks are indented. Messages
                                             are color-coded.

                            ""time-colors"": Messages have timestamps. Nested
                                             blocks are indented. Messages are
                                             color-coded.

                            ""teamcity"":    Adds TeamCity block annotations to
                                             output.

  {DELIMITER} <value>   Allows you to specify a delimiter other than a comma for
                        the comma-delimited arguments.

  {NO_PARALLEL}         Forces each database to be updated in serial. If one
                        database fails to update, no subsequent databases will
                        be attempted.

  {PREVIEW}             Prints which enums were found in the assemblies, but
                        does not replicate them to SQL.

  {HELP}                Prints this help message.
";

        /// <summary>
        /// Executes the EnumToSql program based on command line parameters. Returns true if it executed successfully.
        /// </summary>
        /// <param name="args">An array of command line arguments.</param>
        /// <param name="output">The stream where to send output (for example, Console.Out).</param>
        public static bool Execute(string[] args, TextWriter output)
        {
            Logger logger = null;

            try
            {
                var argsDictionary = GetArgumentsDictionary(args);

                var formatter = GetFormatter(argsDictionary);
                logger = new Logger(output, formatter);

                if (argsDictionary.Count == 0 || argsDictionary.ContainsKey(HELP))
                {
                    logger.Info(s_helpMessage);
                    return true;
                }

                var files = GetAssemblyFiles(argsDictionary);
                var attributeName = argsDictionary.ContainsKey(ATTR) ? argsDictionary[ATTR] : EnumToSqlReplicator.DEFAULT_ATTRIBUTE_NAME;
                var writer = EnumToSqlReplicator.Create(files, logger, attributeName);

                if (!argsDictionary.ContainsKey(PREVIEW))
                {
                    var conns = GetConnectionStrings(argsDictionary);
                    
                    var parallel = !argsDictionary.ContainsKey(NO_PARALLEL);

                    writer.UpdateDatabases(conns, logger, parallel);

                    logger.Info("Updates complete");
                }

                return true;
            }
            catch (Exception ex)
            {
                var types = ex as EnumsToSqlException;
                if (types == null || !types.IsLogged)
                {
                    if (logger != null)
                    {
                        logger.Exception(ex);
                    }
                    else
                    {
                        output.WriteLine(ex);
                        output.WriteLine(ex.StackTrace);
                        output.WriteLine();
                    }
                }

                return false;
            }
        }

        static string[] GetAssemblyFiles(Dictionary<string, string> args)
        {
            if (args.ContainsKey(ASM))
            {
                var asm = args[ASM];
                var files = asm.Split(new[] {GetDelimiter(args)}, StringSplitOptions.RemoveEmptyEntries);

                if (files.Length > 0)
                    return files;
            }

            throw new Exception($"Argument missing: {ASM}");
        }

        static ILogFormatter GetFormatter(Dictionary<string, string> args)
        {
            string formatString;
            if (args.TryGetValue(FORMAT, out formatString))
            {
                switch (formatString.ToLowerInvariant())
                {
                    case "plain":
                        return LogFormatters.Plain;
                    case "time":
                    case "timestamp":
                    case "timestamps":
                        return LogFormatters.Timestamps;
                    case "color":
                    case "colors":
                        return LogFormatters.Colors;
                    case "time-colors":
                    case "times-colors":
                    case "timestamp-color":
                    case "timestamp-colors":
                    case "timestamps-colors":
                        return LogFormatters.TimestampsColors;
                    case "teamcity":
                        return LogFormatters.TeamCity;
                    default:
                        throw new Exception($"Invalid argument value: {FORMAT} \"{formatString}\"");
                }
            }

            return LogFormatters.Plain;
        }

        static string[] GetConnectionStrings(Dictionary<string, string> args)
        {
            if (args.ContainsKey(CONN))
            {
                if (args.ContainsKey(DB))
                    throw new Exception($"Cannot use both {CONN} and {DB}");

                var conns = args[CONN].Split(new[] {GetDelimiter(args)}, StringSplitOptions.RemoveEmptyEntries);

                if (conns.Length == 0)
                    throw new Exception($"No {CONN} values were provided");

                // ensure the list of connections is unique
                var connHash = new HashSet<string>();
                foreach (var conn in conns)
                {
                    var trimmed = conn.Trim();
                    if (connHash.Contains(trimmed))
                        throw new Exception("Duplicate connection strings detected");

                    connHash.Add(trimmed);
                }

                return conns;
            }

            if (args.ContainsKey(DB))
            {
                string server;
                if (!args.TryGetValue(SERVER, out server))
                    server = "localhost";

                var dbs = args[DB].Split(new[] {GetDelimiter(args)}, StringSplitOptions.RemoveEmptyEntries);

                if (dbs.Length == 0)
                    throw new Exception($"No {CONN} values were provided");

                var conns = new string[dbs.Length];
                var dbHash = new HashSet<string>();
                for (var i = 0; i < dbs.Length; i++)
                {
                    var db = dbs[i];

                    var trimmed = db.Trim();
                    if (dbHash.Contains(trimmed))
                        throw new Exception($"Duplicate database \"{trimmed}\"");

                    dbHash.Add(trimmed);

                    conns[i] = $"Integrated Security=true;Initial Catalog={trimmed};server={server.Trim()}";
                }

                return conns;
            }

            throw new Exception($"Must provide either {CONN} or {DB} arguments");
        }

        static string GetDelimiter(Dictionary<string, string> args)
        {
            string delim;
            return args.TryGetValue(DELIMITER, out delim) ? delim : ",";
        }

        static Dictionary<string, string> GetArgumentsDictionary(string[] args)
        {
            var argsDictionary = new Dictionary<string, string>();

            for (var i = 0; i < args.Length; i++)
            {
                var a = args[i];

                if (argsDictionary.ContainsKey(a))
                    throw new Exception($"Duplicate argument \"{a}\"");

                switch (a)
                {
                    case ASM:
                    case CONN:
                    case DB:
                    case SERVER:
                    case ATTR:
                    case FORMAT:
                    case DELIMITER:
                        var value = i + 1 < args.Length ? args[i + 1] : null;
                        if (value == null || value.StartsWith("--"))
                            throw new Exception($"Argument \"{a}\" is missing a value.");

                        argsDictionary[a] = value;
                        i++;
                        break;
                    case NO_PARALLEL:
                    case PREVIEW:
                    case HELP:
                        argsDictionary[a] = null;
                        break;
                    default:
                        throw new Exception($"Unknown argument \"{a}\"");
                }
            }

            return argsDictionary;
        }
    }
}