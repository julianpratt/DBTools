using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq; 
using System.Text;
using System.Data;
using System.Xml;
using System.Xml.XPath;

using Mistware.Utils;
using Dapper;


namespace DBTools
{
    class Program
    {
        static void Main(string[] args)
        {
            string config = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName + ".json";
            
            Config.Setup("DBTools", config);
            Configuration.ReadJsonConfig(config);

            Log.Me.LogFile = "console";
            Log.Me.DebugOn = true;

            Command command = new Command(args);

            if (command.Action == "configure")  
            {
                Connection.Me.Server   = command.Server;
                Connection.Me.Type     = command.Type;
                Connection.Me.Database = null;
                Connection.Me.UserId   = command.Userid;
                Connection.Me.Password = command.Password;
                SaveConfiguration(command.Server, Connection.Me.ConnectionString);
            }
            else if (command.Action == "repository") SaveConfiguration(command.Action, command.FilePath);
            else if (command.Action == "tools")      SaveConfiguration(command.Action, command.FilePath);
            else
            {
                if (command.Action == "create") Connection.Me.Use(command.Server, null); 
                else
                {
                    Connection.Me.Use(command.Server, command.Database); 
                    if (Connection.Me.Type != "MySQL") CheckConnection();
                }
                                            
            
                string repository = Configuration.Get("repository");
                Tools.Path        = Configuration.Get("tools");
           
                if (command.Action == "list") WriteList(ListDatabases(command.Server));
                else
                {
                    if (command.Database.ToUpper() == "ALL")
                    {
                        List<string> l = ListDatabases(command.Server);
                        foreach (string s in l) DoAction(command.Action, command.Server, s, repository, null);
                    }
                    else DoAction(command.Action, command.Server, command.Database, repository, command.Backup);
                }                                 
            }
        }

        public static void DoAction(string action, string server, string database, string repository, string backup)
        {
            if (repository == null)
            {
                Console.WriteLine("Error: DBTools cannot do " + action + ", because the repository has not been specified.");
                System.Environment.Exit(8);
            }

            Builder definition = Builder.Load(repository, database);
            if (definition == null)
            {
                Console.WriteLine("Cannot do " + action + " on database " + database + ". It's definition cannot be found.");
                return;
            }
            database = definition.DatabaseName;
            if (action != "create") Connection.Me.Database = database;
           
            if      (action == "drop")   DropDatabase   (server, database);
            else if (action == "create") CreateDatabase (server, database, repository, definition);
            else 
            {
                List<string> l = ListTables(server, database);
                if (l.Count <= 0)
                {
                    Console.WriteLine("Database " + database + " does not exist or has no tables.");
                    System.Environment.Exit(8);
                }

                // Check that each of the tables in the definition is in the database. 
                foreach (TableDefn t in definition.Tables)
                {
                    if (!l.Contains(t.TableName)) throw new Exception($"Error: Cannot find table {t.TableName} in database."); 
                }
                foreach(string s in l)
                {
                    if (definition.FindTable(s) == null) throw new Exception($"Error: Cannot find table {s} in database definition."); 
                }
                if (definition.Tables.Count != l.Count) throw new Exception($"Error: Database has a different number of tables to the definition."); 

                if      (action == "backup")  BackupDatabase   (server, database, repository, definition);
                else if (action == "restore") RestoreDatabase  (server, database, repository, definition, backup);   
                else if (action == "report")  ReportDatabase   (server, database, repository, definition);
                else if (action == "check")   CheckDatabase    (server, database, repository, definition); 
            }
        }

        public static void WriteList(List<string> l)
        {
            foreach (string s in l) Console.WriteLine(s);
        }

        public static void SaveConfiguration(string key, string value)
        {
            Configuration.Set(key, value);
            Configuration.WriteJsonConfig();
        }

        public static void ShowDatabases(string server)
        {
            List<string> l = ListDatabases(server);
            foreach (string s in l) Console.WriteLine(s);
        }
        
        public static List<string> ListDatabases(string server)
        {
            string sql = null;
            string column = null;
            if (Connection.Me.Type == "MySQL")
            {
                sql = "show databases";
                column = "database";
            }
            else
            {
                sql = "SELECT Name FROM Master.dbo.SysDatabases WHERE Name NOT IN ('master', 'tempdb', 'model', 'msdb');";
                column = "name";
            }

            List<string> l = LoadList(sql, column);

            return l.Where(s => (s != "information_schema") && (s != "sys") && (s != "mysql") && (s != "performance_schema") ).ToList();
        }

        /// <summary>
        /// Execute SQL statement that returns a list of strings. 
        /// </summary>
        /// <param name="query">The sql query to be executed. Parameters may follow a stored procedure name (using name=value,... syntax). If the database name has been given as "odata", then query is the oData url.</param>
        /// <param name="column">The name of the column to capture.</param> 
        /// <returns>List of strings from query.</returns>
        public static List<string> LoadList(string query, string column)
        {
                      
            List<string> l = new List<string>();

            //Fill list database 
            IDbConnection dbConn = Connection.Me.GetConnection();
            try
            {
                dbConn.Open();
                IDbCommand dbCmd = dbConn.CreateCommand();
                dbCmd.CommandType = CommandType.Text;
                dbCmd.CommandText = query;
                   
                using (IDataReader rdr = dbCmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        for (int i = 0; i < rdr.FieldCount; i++)
                        {
                            //Console.WriteLine(rdr.GetName(i) + "=" + rdr.GetValue(i).ToString());
                            if (rdr.GetName(i).ToLower() == column)
                            {   
                                object fldvalue = rdr.GetValue(i);
                                if (fldvalue == DBNull.Value) fldvalue = null;
                                if (fldvalue != null)
                                {
                                    fldvalue = Convert.ChangeType(fldvalue, rdr.GetFieldType(i));
                                    l.Add(fldvalue.ToString());
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("There was a problem loading table from query [" + query + "] - " + ex.Message, ex);
            }
            finally
            {
                dbConn.Close();
                dbConn = null;
            }

            return l;
        }

        public static void BackupDatabase(string server, string database, string repository, Builder definition)
        { 
            
            BackupFile f = new BackupFile(repository, database);
            f.DeleteIfExists();
            f.AppendLine("<?xml version=\"1.0\"?>");
            f.AppendLine("<" + database + ">");
            
            foreach (TableDefn t in definition.Tables)
            {
                BackupTable(t.TableName, f, t);
            }

            f.AppendLine("</" + database + ">");

            Tools.Zip(f.ChangeExt(f.FileNameToday, "zip"), f.FileNameToday);
            File.Delete(f.FileNameToday);
        }

        public static void ReportDatabase(string server, string database, string repository, Builder definition)
        { 
            Console.WriteLine("\nReport for database " + database);
            int width = definition.MaxTableNameLength();

            foreach (TableDefn t in definition.Tables)
            {
                ReportTable(t.TableName, t, width);
            }
        }

        public static void ReportTable(string tablename, TableDefn table, int width)
        {
            string query = "SELECT " + table.SelectCols() + " FROM " + tablename + ";";

            int capacity = 4096;
            StringBuilder sb = new StringBuilder(capacity);
            int rows = 0;
            
            IDbConnection dbConn = Connection.Me.GetConnection();
            try
            {
                dbConn.Open();
                IDbCommand dbCmd = dbConn.CreateCommand();
                dbCmd.CommandType = CommandType.Text;
                dbCmd.CommandText = query;
                   
                using (IDataReader rdr = dbCmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        ++rows;
                        for (int i = 0; i < rdr.FieldCount; i++)
                        {
                            object fldvalue = rdr.GetValue(i);
                            if (fldvalue != DBNull.Value) 
                            {
                                string value = Convert.ChangeType(fldvalue, rdr.GetFieldType(i)).ToString().TrimEnd();
                                string type = table.FindColumn(rdr.GetName(i)).ColumnType;
                                if (type == "BIT")
                                {
                                    if      (value == "0" || value.ToLower() == "false") value = "false"; 
                                    else if (value == "1" || value.ToLower() == "true")  value = "true";  
                                    else Console.WriteLine(value);  
                                }
                                else if (type == "DATETIME") 
                                {
                                    value = value.Left(10);
                                }
                                if (sb.Length + value.Length > capacity)
                                {
                                    capacity = capacity * 2;
                                    sb.EnsureCapacity(capacity); 
                                } 
                                sb.Append(EscapeXML(value));
                            }
                        }
                    }
                }

                if (rows > 0)
                {
                    string hash = MakeHash(sb.ToString());
                    Console.WriteLine(tablename.PadRight(width) + " has " + rows.ToString().PadRight(5) + " rows and hash=" + hash);
                } 
                else Console.WriteLine(tablename.PadRight(width) + " is empty.");
              
               
            }
            catch (Exception ex)
            {
                throw new Exception("There was a problem loading table from query [" + query + "] - " + ex.Message, ex);
            }
            finally
            {
                dbConn.Close();
                dbConn = null;
            }
        }

        public static string MakeHash(string s)
        {
            string hash = "";

            using (System.Security.Cryptography.MD5 md5 = System.Security.Cryptography.MD5.Create())
            {
                byte[] bytes = System.Text.Encoding.ASCII.GetBytes(s);
                byte[] hashBytes = md5.ComputeHash(bytes);

                // Convert the byte array to hexadecimal string
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < hashBytes.Length; i++)
                {
                    sb.Append(hashBytes[i].ToString("X2"));
                }
                hash = sb.ToString();
            }
            return hash;
        }


        public static void CheckDatabase(string server, string database, string repository, Builder definition)
        {
            Console.WriteLine("Checking database " + database);
            bool warn = false;    
            foreach (TableDefn t in definition.Tables)
            {
                warn = CheckTable(t.TableName, t, warn);
            }
            if (warn) Console.WriteLine("Warnings issued. Database does not match definition.\n");
            else      Console.WriteLine("Everything looks OK. As far as I can tell database and defintion match.\n");
        }

        public static bool CheckTable(string tablename, TableDefn table, bool warn)
        {
            string query;
            if (Connection.Me.Type == "MySQL")
            {
                query = "SELECT *  FROM " + tablename + " LIMIT 1;";
            }
            else
            {
                query = "SELECT TOP 1 * FROM " + tablename + ";";
            }

            List<string> fields = new List<string>();
            
            IDbConnection dbConn = Connection.Me.GetConnection();
            try
            {
                dbConn.Open();
                IDbCommand dbCmd = dbConn.CreateCommand();
                dbCmd.CommandType = CommandType.Text;
                dbCmd.CommandText = query;
                   
                using (IDataReader rdr = dbCmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        for (int i = 0; i < rdr.FieldCount; i++)
                        {
                            //Console.WriteLine(rdr.GetName(i) + "=" + rdr.GetValue(i).ToString());
                            string key  = rdr.GetName(i);
                            Type type = rdr.GetFieldType(i);
                            ColumnDefn col = table.FindColumn(key);
                            if (col == null) 
                            {
                                warn = true;
                                Console.WriteLine("Database table " + tablename + " has additional column " + key);
                            }
                            else
                            {
                                if (type.Name != col.CLRType().Name)
                                {
                                    // For some reason MySQL bit fields come through as UInt64, so don't report that 
                                    // and MSSQL renders FLOAT as Doubles, so we'll ignore that too
                                    if (col.ColumnType == "BIT" && type.Name == "UInt64") {}
                                    else if (col.ColumnType == "FLOAT" && type.Name == "Double") {}
                                    else 
                                    {
                                        warn = true;
                                        Console.WriteLine("Table: " + tablename + ", Column: " + key + ", Database Type: " + type.Name + ", Definition: " + col.ColumnType);
                                    }     
                                }                                    
                                fields.Add(key);
                            }
                        }
                    }
                }
                if (fields.Count == 0) Console.WriteLine("Cannot check table " + tablename + " because it is empty.");
                else
                {
                     foreach(ColumnDefn col in table.Columns)
                    {
                        if (!fields.Contains(col.ColumnName)) 
                        {
                            warn = true;
                            Console.WriteLine("Column " + col.ColumnName + " is missing from " + tablename);
                        }
                    }
                }

                return warn;
               
            }
            catch (Exception ex)
            {
                throw new Exception("There was a problem loading table from query [" + query + "] - " + ex.Message, ex);
            }
            finally
            {
                dbConn.Close();
                dbConn = null;
            }
        }
        public static void DropDatabase(string server, string database)
        { 
            if (Connection.Me.Type != "MySQL") 
            {
                Console.WriteLine("Can only use drop action on MySQL databases!");
                System.Environment.Exit(8);
            }

            ExecSQL("USE " + Connection.Me.DefaultDB + "; DROP DATABASE " + database);  
        }

        
        public static void CreateDatabase(string server, string database, string repository, Builder definition)
        { 
            if (Connection.Me.Type != "MySQL") Connection.Me.Database = database; // No default DBs on Azure

            string sql = definition.GetSQL(Connection.Me.Type);
            if (sql == null) throw new Exception($"Cannot setup {database}, no SQL!");
                
            IDbConnection dbConn = Connection.Me.GetConnection();
            try
            {
                // These steps only apply to MySQL servers 
                if (Connection.Me.Type == "MySQL")
                {
                    if (ListDatabases(server).Contains(database)) 
                        throw new Exception("Error in DBTools. Cannot create database [" + database + "] - it is already there!");
                    ExecSQL("USE " + Connection.Me.DefaultDB + "; CREATE DATABASE " + database);  
                }
                
                dbConn.Execute(sql);
            }
            catch (Exception ex)
            {
                string test = "Connection Timeout Expired";
                if (ex.Message.Left(test.Length) == test && Connection.Me.Type != "MySQL")
                {
                    Console.WriteLine("Connection Timeout. Database may have auto-paused. Please wait a minute and try again.");
                    System.Environment.Exit(0);
                }
                throw new Exception("Error in DBTools. There was a problem creating database [" + database + "] - " + ex.Message);
            }
        }

        public static List<string> ListTables(string server, string database)
        {
            string sql = null;
            string column = null;
           
            if (Connection.Me.Type == "MySQL")
            {
                sql = "show tables";
                column = "tables_in_" + database.ToLower();
            }
            else
            {
                sql = "SELECT DISTINCT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE='BASE TABLE';";
                column = "table_name";
            }

            return LoadList(sql, column);
        }
        public static void BackupTable(string tablename, BackupFile file, TableDefn table)
        {
            file.AppendLine("  <" + Pluralizer.Pluralize(tablename) + ">");
            
            string query = "SELECT " + table.SelectCols() + " FROM " + tablename + ";";

            IDbConnection dbConn = Connection.Me.GetConnection();
            try
            {
                dbConn.Open();
                IDbCommand dbCmd = dbConn.CreateCommand();
                dbCmd.CommandType = CommandType.Text;
                dbCmd.CommandText = query;
                   
                using (IDataReader rdr = dbCmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        file.AppendLine("    <" + tablename + ">");
                        for (int i = 0; i < rdr.FieldCount; i++)
                        {
                            //Console.WriteLine(rdr.GetName(i) + "=" + rdr.GetValue(i).ToString());
                            string key = rdr.GetName(i);
                            object fldvalue = rdr.GetValue(i);
                            if (fldvalue != DBNull.Value) 
                            {
                                fldvalue = Convert.ChangeType(fldvalue, rdr.GetFieldType(i));
                                string clrtype = table.FindColumn(key).CLRType().Name;
                                if (fldvalue != null)
                                    file.AppendLine("      <" + key + ">" + FormatValue(fldvalue, clrtype) + "</" + key + ">");
                            }
                        }
                        file.AppendLine("    </" + tablename + ">");
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("There was a problem loading table from query [" + query + "] - " + ex.Message, ex);
            }
            finally
            {
                dbConn.Close();
                dbConn = null;
            }

            file.AppendLine("  </" + Pluralizer.Pluralize(tablename) + ">");
        }

        private static string FormatValue(object value, string clrtype)
        {
            string s = null;

            if (value == null) return "";
            if (clrtype == "Byte") return "BLOB value not exported";

            Type type = value.GetType();
            
            if      (type == typeof(System.String))   s = EscapeXML(((string)value).TrimEnd());
            else if (type == typeof(System.Boolean))  s = (bool)value ? "true" : "false";
            else if (type == typeof(System.DateTime)) s = ((DateTime)value).ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ssZ");
            else 
            {
                if (clrtype == "Boolean")
                {
                    if (value.ToString() == "1") s = "true";
                    else                         s = "false";
                }
                else s = value.ToString();
            }
                                                 
            return s;
        }

        private static string EscapeXML(string s)
        {
            StringBuilder sb = new StringBuilder();
            string valid = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789 !#$%()*+,-./:;=?[]^_{}|~";

            foreach (char c in s)
            {
                if (valid.IndexOf(c) > -1) sb.Append(c);
                else if (c == '<')         sb.Append("&lt;");
                else if (c == '>')         sb.Append("&gt;");
                else if (c == '&')         sb.Append("&amp;");
                else if (c == '\'')        sb.Append("&apos;");
                else if (c == '\\')        sb.Append("&#x5C;");
                else if (c == '"')         sb.Append("&quot;");
                else 
                {
                    string temp = "&#x" + String.Format("{0:X}", Convert.ToUInt32(c)) + ";";
                    if (temp != "&#x1F;" && temp != "&#xB;" && temp != "&#x1;" && temp != "&#xDBC0;" && temp != "&#xDC79;" 
                     && temp != "&#xC;"  && temp != "&#x2;" && temp != "&#xA;") 
                        sb.Append(temp);
                }                      
            }

            return sb.ToString();
        }

        public static void RestoreDatabase(string server, string database, string repository, Builder definition, string backup)
        {
            ExecSQL("USE " + database);

            // Confirm that tables are empty 
            foreach (TableDefn t in definition.Tables)
            {
                if (HasContent(t.TableName)) 
                {
                    Console.WriteLine("Cannot restore database " + database + " some of its tables have content");
                    return;
                }
            }

            Hashtable hash = new Hashtable();

            BackupFile f = new BackupFile(repository, database);

            string archive  = null;
            if (backup == null)
            {
                archive  = f.ArchiveLatest;
            }
            else
            {
                if (backup.Right(4) != ".zip")
                {
                    Console.WriteLine("Cannot restore database " + database + " from " + backup + " - it must be a zip file");
                    return;
                }
                archive  = repository + Tools.PathDelimiter + backup;
                if (!File.Exists(archive))
                {
                    Console.WriteLine("Cannot restore database " + database + " from " + archive + " - it isn't there!");
                    return;
                }
                if (File.Exists(f.ChangeExt(archive, "xml")))
                {
                    Console.WriteLine("Cannot restore database " + database + " from " + archive + " - xml file is also in repository.");
                    return;
                }
            }
            
            Console.WriteLine($"Using {archive}");

            string filename = f.ChangeExt(archive, "xml");
            if (File.Exists(filename)) File.Delete(filename);

            Tools.UnZip(archive, repository);
            if (!File.Exists(filename)) 
            {
                Console.WriteLine("Sleeping for 1000msecs");
                Tools.Sleep(1000);
                if (!File.Exists(filename)) throw new Exception("Backup " + filename + " not in " + archive);
            }

            bool inDatabase = false;

            using (XmlReader rdr = XmlReader.Create(f.OpenStream(filename)))
            {
                while (rdr.Read())
                {
                    if (rdr.NodeType == XmlNodeType.XmlDeclaration && !inDatabase && rdr.Name == "xml")   {}
                    else if (rdr.NodeType == XmlNodeType.Whitespace) {}
                    // Confirm we have the Database start node
                    else if (rdr.NodeType == XmlNodeType.Element && !inDatabase && rdr.Name == database) inDatabase = true;
                    else if (rdr.NodeType == XmlNodeType.Element &&  inDatabase)
                    {
                        TableDefn table = definition.FindTable(rdr.Name);
                        if (table == null) 
                        {
                            Console.WriteLine("Could not find table: " + rdr.Name);
                            System.Environment.Exit(0);
                        }
                        Console.WriteLine("Starting to restore table " + table.TableName);
                        bool idCol = table.HasIdentityColumn && Connection.Me.Type != "MySQL" ;
                        RestoreTable(rdr, table, idCol, hash);
                    }
                    else if (rdr.NodeType == XmlNodeType.EndElement && inDatabase && rdr.Name == database)
                    {
                        // Have found the Database end node
                        break;
                    }
                    else
                    {
                        Console.WriteLine("Unexpected node: " + rdr.Name + " type: " + rdr.NodeType.ToString());
                        System.Environment.Exit(0);
                    }
                }
            }
            
            File.Delete(filename);        
        }

        public static void RestoreTable(XmlReader rdr, TableDefn table, bool idCol, Hashtable hash)
        {
            string tablename = table.TableName;
            string pluralisedTablename = Pluralizer.Pluralize(tablename);

            while (rdr.Read())
            {
                if (rdr.NodeType == XmlNodeType.Whitespace) {}
                else if (rdr.NodeType == XmlNodeType.Element    && rdr.Name == tablename)
                {
                    RestoreRow(rdr, table, idCol, hash);
                }
                else if (rdr.NodeType == XmlNodeType.EndElement && rdr.Name == pluralisedTablename)
                {
                    // End of this table 
                    break;
                }
                else throw new Exception("XML format error in RestoreTable.");
            }
        }

        public static void RestoreRow(XmlReader rdr, TableDefn table, bool idCol, Hashtable hash)
        {
            string tablename = table.TableName;
            bool   inCell = false; 
            string key    = null;
            string value  = null;

            while (rdr.Read())
            {
                if      (rdr.NodeType == XmlNodeType.Whitespace) {}
                else if (rdr.NodeType == XmlNodeType.Element    && !inCell) { key   = rdr.Name; value= null; inCell=true; }
                else if (rdr.NodeType == XmlNodeType.Text       &&  inCell) { value = rdr.Value; }   
                else if (rdr.NodeType == XmlNodeType.EndElement &&  inCell && rdr.Name == key) 
                { 
                    // Save Key and Value
                    ColumnDefn column = table.FindColumn(key); 
                    if (column != null && column.ColumnType.ToUpper() != "BLOB")
                    {
                        string colType = column.CLRType().Name;
                        string colValue = null;
                        if ((value == null || value.Length == 0) && column.IsNullable)
                        {
                            colValue = "null";
                        }
                        else if (colType == "String")
                        {
                            colValue = "'" + RemoveNewline(EscapeSlashQuote(value)) + "'";        
                        }
                        else if (colType == "DateTime")
                        {
                            DateTime dt = value.ToDateTime().ToLocalTime();
                            colValue = "'" + dt.ToString("yyyy-MM-dd'T'HH:mm:ss") + "'";
                        }
                        else if (colType == "Boolean")
                        {
                            if (value == "true") colValue = "1";
                            else                 colValue = "0";
                        }
                        else
                        {
                            colValue = value;
                        }
                        hash.Add(column.ColumnName, colValue);
                    }
                    else throw new Exception("Could not restore column " + key);
                    inCell=false; 
                }
                else if (rdr.NodeType == XmlNodeType.EndElement && !inCell && rdr.Name == tablename)
                {
                    // End node closes row OK
                    break; 
                }
                else throw new Exception("XML format error in RestoreRow.");
            }

            string[] FVArray = AddSeparator(hash, ',');
            string cols = FVArray[0];
            string vals = FVArray[1];
            string sql = "";
            if (idCol) sql += "SET IDENTITY_INSERT " + tablename + " ON; ";
            sql +="INSERT INTO " + tablename + " (" + cols + ") VALUES (" + vals + "); ";
            if (idCol) sql += "SET IDENTITY_INSERT " + tablename + " OFF; ";
            ExecSQL(sql);
            //Clear out Hashtable to handle multiple records
            hash.Clear(); 

        }



        public static bool HasContent(string tablename)
        {
            string query;
            if (Connection.Me.Type == "MySQL")
            {
                query = "SELECT *  FROM " + tablename + " LIMIT 1;";
            }
            else
            {
                query = "SELECT TOP 1 * FROM " + tablename + ";";
            }
            bool hasContent = false;

            IDbConnection dbConn = Connection.Me.GetConnection();
            try
            {
                dbConn.Open();
                IDbCommand dbCmd = dbConn.CreateCommand();
                dbCmd.CommandType = CommandType.Text;
                dbCmd.CommandText = query;
                   
                using (IDataReader rdr = dbCmd.ExecuteReader())
                {
                    while (rdr.Read())
                    {
                        hasContent = true;
                    }
                }               
            }
            catch (Exception ex)
            {
                throw new Exception("There was a problem loading table from query [" + query + "] - " + ex.Message, ex);
            }
            finally
            {
                dbConn.Close();
                dbConn = null;
            }
            return hasContent;
        }


        public static void ExecSQL(string sql)
        {
            string s = sql.Trim();
            if (s.Right(1) != ";") s += ";";

            //Log.Me.Debug($"Executing SQL: {s}");

            IDbConnection dbConn = Connection.Me.GetConnection();
            try
            {
                dbConn.Execute(s);
            }
            catch (Exception ex)
            {
                throw new Exception("Error in DBTools. There was a problem executing sql [" + sql + "] - " + ex.Message);
            }
        }

        public static void CheckConnection()
        {
            string test = "Connection Timeout Expired";
            bool success = false;

            IDbConnection dbConn = Connection.Me.GetConnection();
            for (int i=1; i<4; ++i)
            {
                try
                {
                    dbConn.Execute("USE " + Connection.Me.Database);
                    success = true;
                    break;
                }
                catch (Exception ex)
                {
                    if (ex.Message.Left(test.Length) != test) throw new Exception("Failed in CheckConnection - " + ex.Message);
                    else Console.WriteLine("Connection Timeout. Database may have auto-paused. Will retry in " + i*30 + " seconds.");
                }
                Tools.Sleep(30*1000*i);
            }
            if (!success)
            {
                Console.WriteLine("Failed to get a connection. Please investigate.");
                System.Environment.Exit(8);
            }
        }

        private static string EscapeSlashQuote(string sql)
        {
            char[] to = new char[sql.Length+100];
            int j = 0;
            foreach (char c in sql)
            {
                if (c == '\\') to[j++] = c;
                if (c == '\'') to[j++] = c;
                to[j++] = c;
            }

            if (j == 0) return string.Empty;

            return new string(to, 0, j);
        }

        static string RemoveNewline(string s)
        {
            char[] c = (s + " ").ToCharArray();
            int i, j;

            i = 0;
            j = 0;
            while (i < s.Length) { if (c[i] != '\n') c[j++] = c[i]; i++; }

            return (new string(c, 0, j));
        }

        private static string[] AddSeparator(Hashtable fv, char sep)
        {
            int len = fv.Count;
            int i = 0;
            StringBuilder sbFields = new StringBuilder();
            StringBuilder sbValues = new StringBuilder();
            IDictionaryEnumerator fvEnum = fv.GetEnumerator();
            while (fvEnum.MoveNext())
            {
                sbFields.Append(fvEnum.Key);
                if (i < len - 1) sbFields.Append(sep);
                sbValues.Append(fvEnum.Value);
                if (i < len - 1) sbValues.Append(sep);
                i++;
            }
            string[] output = {sbFields.ToString(), sbValues.ToString()};
            return output;
        }
    }
}
