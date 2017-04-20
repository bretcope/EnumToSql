# EnumToSql

A simple tool for replicating .NET enums to SQL Server.

> Requires .NET Framework 4.6 or later and has only been tested with .NET Framework assemblies.

## Basic Usage

Mark the enums that you want to replicate with a duck-typed [EnumToSql Attribute](#enumtosql-attribute):

```csharp
[EnumToSql("Languages")]
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
Id    Name            DisplayName    Description    IsActive
int   nvarchar(250)   nvarchar(250)  nvarchar(max)  bit
----  --------------  -------------  -------------  --------
0     "English"       "English"      ""             1
1     "German"        "German"       ""             1
2     "Spanish"       "Spanish"      ""             1
```

> See the [DisplayName](#displayname-column) and [Description](#description-column) sections for how to control those column values.

The command is idempotent and is intended to be run as part of a build chain. It will automatically update the SQL tables to match the current state of your code.

#### Wait, how do I get the EnumToSql attribute?

The attribute is [duck-typed](https://en.wikipedia.org/wiki/Duck_typing), so you actually provide your own implementation. EnumToSql simply looks for an attribute with that name. The minimum implementation only requires a `Table` property:

```csharp
class EnumToSqlAttribute : Attribute
{
    string Table { get; }

    internal EnumToSqlAttribute(string table)
    {
        Table = table;
    }
}
```

However, you can control a lot more about the table, such as column names and sizes, by implementing optional properties. You can also pick a different attribute name if you want. See [EnumToSql Attribute](#enumtosql-attribute) for details.

## Documentation

- Columns
    - [Id](#id-column)
    - [Name](#name-column)
    - [DisplayName](#displayname-column)
    - [Descriptions](#description-column)
    - [IsActive](#isactive-column)
- [Integrated Auth](#integrated-auth)
- [Deletion Mode](#deletion-mode)
- [Multiple Assemblies or Databases](#multiple-assemblies-or-databases)
- [Failures](#failures)
- [Logging Formats](#logging-formats)
- [EnumToSql Attribute](#enumtosql-attribute)
- [Programmatic Interface](#programmatic-interface)

### Id Column

The `Id` column represents the integer value of the enum. It also serves as the primary key for the table.

By default, the `Id` column in SQL is the same size as the enum's backing type. In the "Basic Usage" example, the enum was backed by an `int`, therefore the SQL column was an `int`.

SQL Server doesn't allow you to pick between signed and unsigned integers; size matching is as close as we can get.

| Enum's Backing Type | SQL Server Id Column       |
| ------------------- | -------------------------- |
| `byte` or `sbyte`   | `tinyint` (unsigned 8-bit) |
| `short` or `ushort` | `smallint` (signed 16-bit) |
| `int` or `uint`     | `int` (signed 32-bit)      |
| `long` or `ulong`   | `bigint` (signed 64-bit)   |

You can customize the name and type of the Id column by implementing optional properties on the [EnumToSql Attribute](#enumtosql-attribute).

### Name Column

The `Name` column represents the code name for the enum value. The column defaults to type `nvarchar(250) not null`, but its size can be customized via the [EnumToSql Attribute](#enumtosql-attribute). The column can also be disabled or renamed.

### DisplayName Column

EnumToSql looks for the [DisplayNameAttribute](https://docs.microsoft.com/en-us/dotnet/api/System.ComponentModel.DisplayNameAttribute) on enum values to populate the `DisplayName` column. If the attribute is not found, the column falls back to the code name (same as the Name column). The column defaults to type `nvarchar(250) not null`, but its size can be customized via the [EnumToSql Attribute](#enumtosql-attribute). The column can also be disabled or renamed.

### Description Column

EnumToSql can pick up description text one of two ways:

__[DescriptionAttribute](https://docs.microsoft.com/en-us/dotnet/api/System.ComponentModel.DescriptionAttribute)__

```csharp
enum MyEnum
{
    [Description("My description here.")]
    Value = 0,
}
```

or

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

In order for xml summary comments to be picked up, there must be a an xml file in the same directory as the loaded assembly. For example: `c:\path\to\MyAssembly.dll` would need xml file: `c:\path\to\MyAssembly.XML`.

If the Description attribute exists, it takes precedence over xml summary comments.

The column defaults to type `nvarchar(max) not null`, but its size can be customized via the [EnumToSql Attribute](#enumtosql-attribute). The column can also be disabled or renamed.

### IsActive Column

For each row, IsActive column is set to 1 (true) if:

- The enum value still exists in code, and
- It is not marked with the `[Obsolete]` attribute.

The column can be renamed or disabled via the [EnumToSql Attribute](#enumtosql-attribute).

### Integrated Auth

If you're using integrated auth, you can use the `--db` and `--server` arguments instead of providing a full connection string with `--conn`.

```
> EnumToSql.exe --asm c:\path\to\asm.dll --db MyDatabase --server MyServer
```

> `--server` is optional and defaults to "localhost".

### Deletion Mode

If you delete an enum value from code, it may still exist as a row in a SQL table. How EnumToSql acts in this situation is controlled by the optional `DeletionMode` property on the [EnumToSql Attribute](#enumtosql-attribute). It has four possible values:

- `MarkAsInactive`:  this is the default. Deleted values are marked as inactive in SQL (`IsActive = 0`). Note: if you use this value, the [IsActive column](#isactive-column) must not be disabled.
- `DoNothing`: deleted values are ignored.
- `Delete`: values deleted in code are also deleted from SQL Server. If the delete fails (e.g. foreign key violations), it is treated as a [failure](#failures).
- `TryDelete`: attempts to delete from SQL, but failures due to foreign key violations are treated as warnings.

> __Note__: if you delete the entire enum itself, you'll need to manually drop the table from SQL. This section only applies to individual values of an enum.
>

### Multiple Assemblies or Databases

The `--asm`, `--conn`, and `--db` all accept a comma-delimited list, rather than just a single value. Multiple databases are updated in parallel (unless `--no-parallel` was set).

>  Do not include any whitespace around the comma. `--db Database1,Database2` is correct. `--db Database1, Database2` will fail.

The delimiter defaults to a comma, but can be set to any arbitrary string using the `--delimiter` argument.

### Failures

EnumToSql does not run updates inside a transaction. This helps simplify the code and minimize locking, but has the obvious disadvantage that it could leave a database in a partially updated state. In practice, the impact is minimal because the tool is idempotent, so a successful run will typically correct a previous failed run.

EnumToSql will stop attempting to updating a database after the first failure. When updating multiple databases in parallel, it will attempt to update each database, even if a prior database failed. If parallel is disabled (with `--no-parallel`), EnumToSql will not attempt to update the next database if the prior one failed.

### Logging Formats

The logging output is relatively plaintext by default. However, there are a few built-in formatters including colors, timestamps, and a TeamCity-specific formatter. Use the `--help` argument for more information, or see [LogFormatters.cs](https://github.com/bretcope/EnumToSql/master/EnumToSql/LogFormatters.cs). Feel free to implement your own formatter and submit a pull request.

### EnumToSql Attribute

By default, EnumToSql looks for an attribute named "EnumToSql". You can customize the name using the `--attr` argument (e.g. `--attr CustomName`). The "Attribute" suffix is automatically appended to the name, unless it already ends with Attribute.

> __Example use case__: You have some enums which need to replicate to database A, and other enums in the same assembly which need to replicate to database B. To accomplish this, use two different attribute names (one for each database) and simply run EnumToSql twice with different `--attr` and `--conn` arguments.

The EnumToSql attribute requires a `Table` property, but there are several optional properties and one optional method which allow you to control several things about the table, including column names, sizes, [deletion mode](#deletion-mode), and more. Here's a complete list of members EnumToSql will look for on the attribute:

```csharp
class EnumToSqlAttribute : Attribute
{
    // REQUIRED
    // The name of the table where the enum should be replicated to.
    string Table { get; }
    
    // The schema which the table lives on. Defaults to "dbo" if property is
    // not implemented.
    string Schema { get; }

    // Controls what happens when an enum value no longer exists in code, but
    // still exists as a database row. See the "Deletion Mode" section in this
    // README for more information. Defaults to "MarkAsInactive" if property is
    // not implemented. 
    string DeletionMode { get; }
    
    // Controls the name of the "Id" column.
    string IdColumnName { get; }
    
    // The size (in bytes) of the "Id" column in SQL. This must not be
    // less than the size of the enum's backing type. Valid values are
    // 1, 2, 4, or 8. If the property is missing or returns zero,
    // defaults to the size of the backing type.
    int IdColumnSize { get; }
    
    // Controls the name of the "Name" column.
    string NameColumn { get; }

    // Controls the size (in 2-byte chars) of the "Name" column. Must be big
    // enough to fit the name of each of the enum's values. Defaults to 250 if
    // the property is not implemented. Use int.MaxValue for "max".
    int NameColumnSize { get; }

    // Enables or disables the "Name" column. Defaults to true if the property
    // is not implemented. 
    bool NameColumnEnabled { get; }

    // Controls the name of the "DisplayName" column.
    string DisplayNameColumn { get; }

    // Controls the size (in 2-byte chars) of the "DisplayName" column. Must be
    // big enough to fit the display name of each of the enum's values.
    // Defaults to 250 if the property is not implemented. Use int.MaxValue for
    // "max".
    int DisplayNameColumnSize { get; }

    // Enables or disables the "DisplayName" column. Defaults to true if the
    // property is not implemented. 
    bool DisplayNameColumnEnabled { get; }

    // Controls the name of the "Description" column.
    string DescriptionColumn { get; }

    // Controls the size (in 2-byte chars) of the "Description" column. Must be
    // big enough to fit the description of each of the enum's values. Defaults
    // to int.MaxValue if the property is not implemented.
    int DescriptionColumnSize { get; }

    // Enables or disables the "Description" column. Defaults to true if the
    // property is not implemented.
    bool DescriptionColumnEnabled { get; }

    // Controls the name of the "IsActive" column.
    string IsActiveColumn { get; }

    // Enables or disables the "IsActive" column. Defaults to true if the
    // property is not implemented. Note: if you set this property to false,
    // you must implement the "DeletionMode" property and set it to an accepted
    // value other than "MarkAsInactive".
    bool IsActiveColumnEnabled { get; }

    // Called before any of the above properties are accessed. The
    // argument "targetEnum" is the type of the enum which the attribute
    // was applied to.
    void Setup(Type targetEnum)
    {
        // Implement this method if you want to dynamically generate
        // properties, such as "Table" based on the enum the attribute was
        // applied to.
    }
}
```

Note, __the existing SQL schema needs to match _exactly_ what EnumToSql expects__ for any given table. EnumToSql will create a table if it doesn't already exist, but if the table already exists, and the list of columns (name/size/type) doesn't match what was expected, it will be reported as a [failure](#failures).

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
var enumToSql = EnumToSqlReplicator.Create(assemblies, logger);
enumToSql.UpdateDatabases(connectionStrings, logger);
```

An exception will be thrown if anything fails, but most useful information is still sent to the logger.

You can also call invoke the command line interface programmatically via the static [Cli class](https://github.com/bretcope/EnumToSql/blob/master/EnumToSql/Cli.cs). In fact, here is the entire implementation of EnumToSql's main method:

```csharp
static int Main(string[] args)
{
    return Cli.Execute(args, Console.Out) ? 0 : 1;
}
```

