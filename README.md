# EnumToSql

A simple tool for replicating .NET enums to SQL Server.

> Requires .NET Framework 4.6 or later and has only been tested with .NET Framework assemblies.

## Basic Usage

Just mark the enums that you want to replicate:

```csharp
[EnumSqlTable("Languages")]
enum Language
{
    English = 0,
    German = 1,
    Spanish = 2,
}
```

> __Note__: it is strongly recommended that you use explicit integer values (Name = integer) for enums you plan to replicate to SQL Server.

And then run the command line tool (acquired from building the project in this repo):

```
> EnumToSql.exe --asm "c:\path\to\MyAssembly.dll" --conn "SQL Server connection string"
```

This will create a table called "Languages":

```
Id    Name            Description    IsActive
int   nvarchar(250)   nvarchar(max)  bit
----  --------------  -------------  --------
0     "English"       ""             1
1     "German"        ""             1
2     "Spanish"       ""             1
```

The command is idempotent and is intended to be run as part of a build chain. It will automatically update the SQL tables to match the current state of your code.

#### Wait, where is the EnumSqlTable attribute?

Actually, you provide your own implementation. EnumToSql simply looks for an attribute with that name applied to one or more enums. The minimal required implementation is:

```csharp
class EnumSqlTableAttribute : Attribute
{
    internal string TableName { get; }

    internal EnumSqlTableAttribute(string tableName)
    {
        TableName = tableName;
    }
}
```

However, there are optional properties you can choose to implement. You can also tell EnumToSql to look for a different attribute name. See [EnumSqlTable Attribute](#enumsqltable-attribute) for details.

## Documentation

- [Integrated Auth](#integrated-auth)
- [Id Column](#id-column)
- [Descriptions](#descriptions)
- [IsActive](#isactive)
- [Deletion Mode](#deletion-mode)
- [Multiple Assemblies or Databases](#multiple-assemblies-or-databases)
- [Failures](#failures)
- [Logging Formats](#logging-formats)
- [EnumSqlTable Attribute](#enumsqltable-attribute)
- [Programmatic Interface](#programmatic-interface)

### Integrated Auth

If you're using integrated auth, you can use the `--db` and `--server` arguments instead of providing a full connection string with `--conn`.

```
> EnumsToSql.exe --asm c:\path\to\asm.dll --db MyDatabase --server MyServer
```

> `--server` is optional and defaults to "localhost".

### Id Column

By default, the `Id` column in SQL is the same size as the enum's backing type. In the "Basic Usage" example, the enum was backed by an `int`, therefore the SQL column was an `int`.

SQL Server doesn't allow you to pick between signed and unsigned integers; size matching is as close as we can get.

| Enum's Backing Type | SQL Server Id Column       |
| ------------------- | -------------------------- |
| `byte` or `sbyte`   | `tinyint` (unsigned 8-bit) |
| `short` or `ushort` | `smallint` (signed 16-bit) |
| `int` or `uint`     | `int` (signed 32-bit)      |
| `long` or `ulong`   | `bigint` (signed 64-bit)   |

You can customize the name and type of the Id column by implementing optional properties on the [EnumSqlTable Attribute](#enumsqltable-attribute).

> Note, the SQL schema needs to match _exactly_ what EnumToSql expects for any given table. If you do anything, like change a type or add a column, it will result in a failure.

### Descriptions

EnumToSql can pick up description text one of two ways:

__XML Summary Comments__

```csharp
enum MyEnum
{
    /// <summary>
    /// My description here.
    /// </summary>
    Value = 0,
}
```

or

__[System.ComponentModel.DescriptionAttribute](https://msdn.microsoft.com/en-us/library/system.componentmodel.descriptionattribute(v=vs.110).aspx)__

```csharp
enum MyEnum
{
    [Description("My description here.")]
    Value = 0,
}
```

In order for xml summary comments to be picked up, there must be a an xml file in the same directory as the loaded assembly. For example: `c:\path\to\MyAssembly.dll` would need xml file: `c:\path\to\MyAssembly.XML`.

If both xml summary comments and the Description attribute exists, the xml summary comments take precedence.

### IsActive

For each row, IsActive column is set to 1 (true) if:

- The enum value still exists in code, and
- It is not marked with the `[Obsolete]` attribute.

### Deletion Mode

If you delete an enum value from code, it may still exist as a row in a SQL table. How EnumToSql acts in this situation is controlled by the `--delete-mode` argument. It has four possible values:

- `mark-inactive`:  this is the default. Deleted values are marked as inactive in SQL (`IsActive = 0`).
- `do-nothing`: deleted values are ignored.
- `delete`: values deleted in code are also deleted from SQL Server. If the delete fails (e.g. foreign key violations), it is treated as a fatal error.
- `try-delete`: attempts to delete from SQL, but failures due to foreign key violations are treated as warnings.

> __Note__: if you delete the entire enum itself, you'll need to manually drop the table from SQL. This section only applies to individual values of an enum.
>
> __Note__: There is probably a strong case to be made for allowing the EnumSqlTable attribute to override the deletion mode. Open an issue if you have a use case.

### Multiple Assemblies or Databases

The `--asm`, `--conn`, and `--db` all accept a comma-delimited list rather than a single value. Multiple databases are updated in parallel (unless `--no-parallel` was set).

The delimiter defaults to a comma, but can be set to any arbitrary string using the `--delimiter` argument.

### Failures

EnumToSql does not run updates inside a transaction. This helps simplify the code and minimize locking, but has the obvious disadvantage that it could leave a database in a partially updated state. In practice, the impact is minimal because:

- The tool is idempotent, so a successful run will typically correct a previous failed run.
- Deletions (most likely to fail) are run last, after inserts and updates.

EnumToSql will stop attempting to updating a database after the first failure. When updating multiple databases in parallel, all databases are attempted, even if a prior database failed. If parallel is disabled (with `--no-parallel`), EnumToSql will not attempt to update the next database if the prior one failed.

### Logging Formats

The logging output is relatively plaintext by default. However, there are a few built-in formatters including colors, timestamps, and a TeamCity-specific formatter. Use the `--help` argument for more information, or see [LogFormatters.cs](https://github.com/bretcope/EnumToSql/master/EnumToSql/LogFormatters.cs). Feel free to implement your own formatter and submit a pull request.

### EnumSqlTable Attribute

By default, EnumsToSql looks for an attribute named "EnumSqlTable". You can customize the name using the `--attr` argument (e.g. `--attr CustomName`). The "Attribute" suffix is automatically appended to the name, unless it already ends with Attribute.

EnumToSql looks for the following properties on the attribute:

- __TableName__ `string` (required):  The name of the table where the enum should be replicated to. This must never be null or empty.
- __SchemaName__ `string` (optional): The schema which the table lives on. If this property is missing, or returns null/empty, defaults to "dbo".
- __IdColumnSize__ `int` (optional): The size (in bytes) of the "Id" column in SQL. This must not be less than the size of the enum's backing type. Valid values are 1, 2, 4, or 8. If the property is missing or returns zero, defaults to the size of the backing type.
- __IdColumnName__ `string` (optional): Allows you to specify a column name other than "Id" for the first column. If the property is missing, or returns null/empty, defaults to "Id".

> I'd be interested in exploring the ability to dynamically generate a table name based on the enum's name. Open an issue if you're interested.

### Programmatic Interface

Although EnumToSql is primarily designed as a command line interface, it can also be used as a library. You can add a reference to the exe, or recompile the project as a library if you'd prefer.

The main class is [EnumToSqlReplicator](https://github.com/bretcope/EnumToSql/blob/master/EnumToSql/EnumToSqlReplicator.cs). Here's a minimal example:

```csharp
var assemblies = new []
{
    @"c:\path\to\AssemblyOne.dll",
    @"c:\path\to\AssemblyTwo.dll",
};

var connectionStrings = new []
{
    "Integrated Security=true;Server=localhost;Initial Catalog=DatabaseOne",
    "Integrated Security=true;Server=localhost;Initial Catalog=DatabaseTwo",
    "Integrated Security=true;Server=localhost;Initial Catalog=DatabaseThree",
};

var logger = new Logger(Console.Out, LogFormatters.Plain);
var writer = EnumToSqlReplicator.Create(assemblies, logger);
writer.UpdateDatabases(connectionStrings, DeletionMode.TryDelete, logger);
```

An exception will be thrown if anything fails, but most useful information is still sent to the logger.

You can also call invoke the command line interface programmatically via the static [Cli class](https://github.com/bretcope/EnumsToSql/blob/master/EnumToSql/Cli.cs). In fact, here is the entire implementation of EnumToSql's main method:

```csharp
static void Main(string[] args)
{
    var success = Cli.Execute(args, Console.Out);
    Environment.Exit(success ? 0 : 1);
}
```

