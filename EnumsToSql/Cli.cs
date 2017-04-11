using System;
using System.Collections.Generic;
using System.IO;

namespace EnumsToSql
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
        const string DELETED = "--deleted";
        const string FORMAT = "--format";
        const string PREVIEW = "--preview";
        const string HELP = "--help";

        /// <summary>
        /// The name of the executable (used in the help message).
        /// </summary>
        public static string ExeName { get; set; } = "EnumsToSql";

        /// <summary>
        /// Executes the EnumToSql program based on command line parameters. Returns true if it executed successfully.
        /// </summary>
        /// <param name="args">An array of command line arguments.</param>
        /// <param name="output">The stream where to send output (for example, Console.Out).</param>
        public static bool Execute(string[] args, TextWriter output)
        {
            try
            {
                var argsDictionary = GetArgumentsDictionary(args);

                if (argsDictionary.Count == 0 || argsDictionary.ContainsKey(HELP))
                {
                    WriteHelp(output);
                    return true;
                }

                var files = GetAssemblyFiles(argsDictionary);
                //var format = GetFormat(argsDictionary); // todo: fix logger to actually use format
                var attributeName = argsDictionary.ContainsKey(ATTR) ? argsDictionary[ATTR] : EnumsToSqlWriter.DEFAULT_ATTRIBUTE_NAME;
                var writer = EnumsToSqlWriter.Create(files, Console.Out, attributeName);

                if (!argsDictionary.ContainsKey(PREVIEW))
                {
                    var conns = GetConnectionStrings(argsDictionary);

                    var deletionMode = GetDeletionMode(argsDictionary);
                    // todo: make parallel version
                    foreach (var conn in conns)
                    {
                        writer.UpdateDatabase(conn, deletionMode, Console.Out);
                    }

                    Console.WriteLine("Updates complete");
                }

                return true;
            }
            catch (Exception ex)
            {
                output.WriteLine(ex);
                output.WriteLine();
                WriteHelp(output);

                return false;
            }
        }

        static string[] GetAssemblyFiles(Dictionary<string, string> args)
        {
            if (args.ContainsKey(ASM))
            {
                var asm = args[ASM];
                var files = asm.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);

                if (files.Length > 0)
                    return files;
            }

            throw new Exception($"Argument missing: {ASM}");
        }

        static OutputFormat GetFormat(Dictionary<string, string> args)
        {
            string formatString;
            if (args.TryGetValue(FORMAT, out formatString))
            {
                switch (formatString.ToLowerInvariant())
                {
                    case "none":
                        return OutputFormat.None;
                    case "teamcity":
                        return OutputFormat.TeamCity;
                    default:
                        throw new Exception($"Invalid argument value: {FORMAT} \"{formatString}\"");
                }
            }

            return OutputFormat.None;
        }

        static DeletionMode GetDeletionMode(Dictionary<string, string> args)
        {
            string deleteString;
            if (args.TryGetValue(FORMAT, out deleteString))
            {
                switch (deleteString.ToLowerInvariant())
                {
                    case "mark-inactive":
                        return DeletionMode.MarkAsInactive;
                    case "do-nothing":
                        return DeletionMode.DoNothing;
                    case "delete":
                        return DeletionMode.Delete;
                    case "try-delete":
                        return DeletionMode.TryDelete;
                    default:
                        throw new Exception($"Invalid argument value: {DELETED} \"{deleteString}\"");
                }
            }

            return DeletionMode.MarkAsInactive;
        }

        static string[] GetConnectionStrings(Dictionary<string, string> args)
        {
            if (args.ContainsKey(CONN))
            {
                if (args.ContainsKey(DB))
                    throw new Exception($"Cannot use both {CONN} and {DB}");

                var conns = args[CONN].Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries);

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

                var dbs = args[DB].Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

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

        static void WriteHelp(TextWriter output)
        {
            output.Write($@"Usage: {ExeName} [OPTIONS]+

  Replicates enums in the selected assemblies to SQL Server.

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
                        replication to SQL Server. Defaults to ""{EnumsToSqlWriter.DEFAULT_ATTRIBUTE_NAME}"".

  {DELETED} <value>     Controls what happens when an enum value no longer
                        exists in code, but still exists as a database row.

                        Possible values:

                            ""mark-inactive"": (default) Sets IsActive = 0 for
                                             these rows.

                            ""do-nothing"":    These rows are ignored.

                            ""delete"":        Deletes these rows from the
                                             database. If the rows cannot be
                                             deleted, this is treated as a
                                             failure.

                            ""try-delete"":    Attempts to delete the rows, but
                                             failures due to constraint
                                             (foreign key) violations are 
                                             treated as warnings.

  {FORMAT} <value>      Sets the output format. Possible values:

                            ""none"":     (default) No special formatting.

                            ""teamcity"": Adds TeamCity block annotations to output.

  {PREVIEW}             Prints which enums were found in the assemblies, but
                        does not replicate them to SQL.

  {HELP}                Prints this help message.
");
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
                    case DELETED:
                    case FORMAT:
                        var value = i + 1 < args.Length ? args[i + 1] : null;
                        if (value == null || value.StartsWith("--"))
                            throw new Exception($"Argument \"{a}\" is missing a value.");

                        argsDictionary[a] = value;
                        i++;
                        break;
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