DBTools
=======

DBTools is a command line utility to assist with database management. It assumes that development databases are served from MySQL and production databases are on Azure MS SQL (currently these are the only two types: "MySQL" and "Azure"). 

Each database has a definition of its structure (tables, columns, etc), which is used to: create the database (with empty tables), backup the database and restore the database. Backups are stored in xml, which DBTools compresses to zip files (they are highly compressible, and the space saving is worthwhile). Each backup is identified by the database name and the date it was taken. An external zip tool (7za) is used to compress and decompress backup files.

Definitions and backups are stored in a Repository (folder). The executables DBTools and 7za are stored in a Tools folder, that is on your path (e.g. HOME/Tools). 

Configuration (DBTools.json) is stored in the same folder as the executable and it stores the configuration (Repository location, Tools location and database servers). 

It is helpful to distinguish between production, staging and development databases, by giving them different names (e.g. by appending 'Test' or 'Dev' to the name). This avoids any confusion between production and development data. When restoring production data to a staging database, it is necessary to rename the database in the xml file. 

The main reason for introducing a new definition syntax was the requirement to achieve portability between MS SQL and MySQL databases. DBTools uses the definition to generate the create statements for each type of database. Examining the DatabaseBuilder class will give rapid insight into what is happening.   



Documentation
--------

Actions available:

```
help                           - display help information
configure  server type uid pwd - Save server connection data
list       server              - List the databases on a server
create     server database     - Create a database from its definition
backup     server database     - Copy data from database to an xml file
restore    server database     - Fill database with data from an xml file
drop       server database     - Delete a database
report     server database     - Report row count and md5 hash for each table in a database
check      server database     - Check a database against its definition
repository filepath            - Save location of repository
tools      filepath            - Save location of external tools
```  

Notes:

1. configure takes the name of the server, its type ('azure' or 'mysql'), and a userid and password to access the server. These are stored in DBTools.json and used to form the connection string.
2. Azure server name can be specified in configure as just its name, the connection string that is stored will wrap it with 'tcp:' and '.database.windows.net,1433' - Azure connection strings are recognised because they are formatted thus. Azure servers are assumed to be MSSQL.
3. create will not overwrite an existing database (use drop first). For Azure databases create will only create the tables, it will not create the empty database, which must be done using the Azure portal.
4. backup and restore will only work if there is a corresponding definition and will only backup the tables and columns specified in the definition.
5. create, backup, restore, drop, report and check will act on all a server's databases if database is 'all'
6. restore will not override data (tables must be empty) - use drop first. By default restore uses the latest backup for that database, but an alternative zip file with another backup can be specified on the command line after the database name - all backups are zipped in the repository.
7. drop will not delete an Azure database (as these are assumed to be in production). Use the Azure Portal to delete Azure databases.
8. Do not have servers called 'repository' or 'tools' - names are reserved.
9. Do not have a database called 'all' - this name is reserved.


No documentation has been provided yet for the syntax of database definitions. An example database has been provided, so that developers with a modicum of SQL knowledge should be able to make a start with using it for their own databases.    


Usage
--------

This application requires the [.NET Core 3.1 SDK](https://dotnet.microsoft.com/download) to be downloaded and installed (preferably on the server where it is to run).

Having installed the SDK: download the application from GitHub (either using git or by downloading and unzipping the zip file), and then build and publish it.

DBTools is dependent on these nuget packages: Dapper (v2.0.30), System.Data.SqlClient (v4.8.0), MySql.Data (v8.0.19) and Mistware.Utils (v1.0.2). 

It may be necessary to change the RuntimeIdentifier in the csproj (which is currently set to "osx-x64"). Search "dotnet core rid catalog" for the right setting for your machine (e.g. Windows users will need "win-x64").

```
git clone https://github.com/julianpratt/DBTools.git
dotnet restore
dotnet build 
dotnet publish
```

Copy the executable from the publish folder (which dotnet will tell you) to a folder on your path (I have a folder called Tools for useful console apps and scripts). 

DBTools is run by issuing the command:

```
dbtools
```

Ensure the 7za executable is also in the Tools folder and tell DBTools where that is:

```
dbtools tools full-path
```   

Create the Repository folder and tell DBTools where it is:

```
dbtools repository full-path
```

An example database is provided, which can be setup by compressing Example-Data.xml (to Example-Data.zip), copying Example.dbd and Example-Data.zip to the Repository and then:

```
dbtools configure server type uid pwd
dbtools create server example
dbtools restore server example example-data.zip
```


7za
--------------------

DBTools needs access to a copy of 7za to zip and unzip the backups. This is part of p7zip (the port of the command line version of 7-Zip to Linux/Posix). Go to https://www.7-zip.org/, follow the link to p7zip (https://sourceforge.net/projects/p7zip/) and download it, unzip the tar file, in a console change to the root of p7zip and use make (on OSX you will need Xcode and its command line tools for this to work). Ignore the copious warnings and find a copy of 7za in the bin folder. This should be moved to Tools.


Testing
---------------------
DBTools has been tested against the Example database. It has also been used to backup and restore a copy of the production Report Distribution database, with checksums confirming the data was transferred with 100% accuracy.
