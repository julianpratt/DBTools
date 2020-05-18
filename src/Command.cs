using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Diagnostics; 
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.Xml;
using System.Xml.XPath;
using Mistware.Utils;

namespace DBTools
{
    /// Process the incoming command string, setting properties in this class 
    public class Command
    {
        /// The requested action
        public string Action   { get; }

        /// The requested server (aka connection string)
        public string Server   { get; }

        /// The requested Database
        public string Database { get; }

        /// The name of the backup file to use in restore
        public string Backup   { get; } 

        /// The SQL Server Type (azure or mysql)
        public string Type     { get; }

         /// The Userid
        public string Userid   { get; }

         /// The Password
        public string Password { get; }

         /// The FilePath
        public string FilePath { get; }

        /// The argument string is processed in the class constructor
        public Command(string[] args)
        {
            string[] actions = {"help", "configure", "list", "create", "backup", "restore", "drop", "report", "check", "repository", "tools" };

            Args a = new Args(args);
            Action = a.Action;  

            if (Action == "help") Help();

            if (Action == null || !Contains(Action, actions)) Usage();

            Server   = null;
            Database = null;
            Backup   = null;
            Type     = null;
            Userid   = null;
            Password = null;
            FilePath = null;

            if (Action == "configure")
            {
                if (a.Parameters.Count != 4) Usage();
                Server   = a.Parameters[0];
                Type     = a.Parameters[1];
                Userid   = a.Parameters[2];
                Password = a.Parameters[3];
            }
            else if (Action == "list")
            {
                if (a.Parameters.Count != 1) Usage();
                Server   = a.Parameters[0];
            } 
            else if (Action == "restore")
            {
                if (a.Parameters.Count < 2 || a.Parameters.Count > 3) Usage();
                Server   = a.Parameters[0];
                Database = a.Parameters[1];
                if (a.Parameters.Count == 3) Backup = a.Parameters[2];
            } 
            else if (Action == "create" || Action == "backup" || Action == "drop" || Action == "report" || Action == "check")
            {
                if (a.Parameters.Count != 2) Usage();
                Server   = a.Parameters[0];
                Database = a.Parameters[1];
            } 
            else
            {
                if (a.Parameters.Count != 1) Usage();
                FilePath = a.Parameters[0];
            }

            Server = Server.ToLower();

             if (Server != null && (Server.ToLower() == "repository" || Server.ToLower() == "tools" ))
                throw new Exception("Cannot use 'repository' or 'tools' as a server name!");
        }
        private bool Contains(string key, string[] list)
        {
            return Contains(key, list.ToList());
        }
        private bool Contains(string key, List<string> list)
        {    
            foreach (string item in list) if (item.ToLower() == key.ToLower()) return true;
            return false;          
        }
        private void Usage()
        {
            Console.WriteLine("Usage: dbtools help");
            Console.WriteLine("       dbtools configure  server type userid password");
            Console.WriteLine("       dbtools list       server");
            Console.WriteLine("       dbtools create     server database");
            Console.WriteLine("       dbtools backup     server database");
            Console.WriteLine("       dbtools restore    server database (backup)");
            Console.WriteLine("       dbtools drop       server database");
            Console.WriteLine("       dbtools report     server database");
            Console.WriteLine("       dbtools check      server database");
            Console.WriteLine("       dbtools repository filepath");
            Console.WriteLine("       dbtools tools      filepath");
            
            System.Environment.Exit(0);
        }

        private void Help()
        {
            Console.WriteLine("");
            Console.WriteLine("DBTools Help");
            Console.WriteLine("============");
            Console.WriteLine("");
            Console.WriteLine("This tool will: create a new database from its definition (which is stored");
            Console.WriteLine("in repository and has the same name as the database and an extension of .dbd),");
            Console.WriteLine("preserve the contents of a database in an xml file and then use that xml file");
            Console.WriteLine("to recreate the database. Definitionsand backup files are all stored in a");
            Console.WriteLine("repository. An external zip tool (7za) is used to compress and decompress");
            Console.WriteLine("backup files. Configuration is stored in the same folder as the executable.");
            Console.WriteLine("");
            Console.WriteLine("Actions available:");
            Console.WriteLine("");
            Console.WriteLine("  help                           - display this information");
            Console.WriteLine("  configure  server type uid pwd - Save server connection data");
            Console.WriteLine("  list       server              - List the databases on a server");
            Console.WriteLine("  create     server database     - Create a database from its definition");
            Console.WriteLine("  backup     server database     - Copy data from database to a text file");
            Console.WriteLine("  restore    server database     - Fill database with data from text file");
            Console.WriteLine("  drop       server database     - Delete a database");
            Console.WriteLine("  report     server database     - Report row count and md5 hash for each");
            Console.WriteLine("                                   table in a database");
            Console.WriteLine("  check      server database     - Check a database against its definition");
            Console.WriteLine("  repository filepath            - Save location of repository");
            Console.WriteLine("  tools      filepath            - Save location of external tools");
            Console.WriteLine("");
            Console.WriteLine("Notes:");
            Console.WriteLine("  1. configure takes the name of the server, its type ('azure' or 'mysql'),");
            Console.WriteLine("     and a userid and password to access the server.");
            Console.WriteLine("     These are stored in DBTools.json and used to form the connection string.");
            Console.WriteLine("  2. Azure server name can be specified in configure as just its name, the");
            Console.WriteLine("     connection string that is stored will wrap it with 'tcp:' and");
            Console.WriteLine("     '.database.windows.net,1433' - Azure connection strings are recognised");
            Console.WriteLine("     because they are formatted thus. Azure servers are assumed to be MSSQL.");
            Console.WriteLine("  3. create will not overwrite an existing database (use drop first).");
            Console.WriteLine("     For Azure databases create will only create the tables, it will not");
            Console.WriteLine("     create the empty database, which must be done using the Azure portal.");
            Console.WriteLine("  4. backup and restore will only work if there is a corresponding definition");
            Console.WriteLine("     and will only backup the tables and columns specified in the definition.");
            Console.WriteLine("  5. create, backup, restore, drop, report and check will act on all a");
            Console.WriteLine("     server's databases if database is 'all'");
            Console.WriteLine("  6. restore will not override data (tables must be empty) - use drop first");
            Console.WriteLine("     By default restore uses the latest backup for that database, but an");
            Console.WriteLine("     alternative zip file with another backup can be specified on the command");
            Console.WriteLine("     line after the database name - all backups are zipped in the repository.");
            Console.WriteLine("  7. drop will not delete an Azure database (as these are assumed to be in ");
            Console.WriteLine("     production). Use the Azure Portal to delete Azure databases.");
            Console.WriteLine("  8. Do not have servers called 'repository' or 'tools' - names are reserved.");
            Console.WriteLine("  9. Do not have a database called 'all' - this name is reserved.");
            Console.WriteLine("");
            
            System.Environment.Exit(0);
        }
    }
}