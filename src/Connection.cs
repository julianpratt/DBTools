using System;
using System.Data;
using System.Data.SqlClient;
using MySql.Data.MySqlClient;

using Mistware.Utils;

namespace DBTools
{
    /// Make a database the current database in use and look up its connection string. 
    public class Connection
    {
        private Connection() { }

        private static Connection _me;

        /// Returns singleton instance of Connection class. 
        public static Connection Me
        {
            get
            {
                if (_me == null) _me = new Connection();
                return _me;
            }
        }

        /// <summary>
        /// Set the database to be used.
        /// </summary>
        /// <param name="server">The name of the database server, that the database is hosted on.</param>
        /// <param name="database">The name of the database itself.</param>
        public void Use(string server, string database)
        {
            string connect = Configuration.Get(server);
            if (connect == null)
            { 
                Console.WriteLine("Cannot use server " + server + ". It has not been configured.");
                System.Environment.Exit(8);
            }

            ParseConnect(connect);

            Database = database;
        }

        /// Server Name 
        public string Server { 
            get { return server; }
            set
            {
                string stem = ".database.windows.net,1433";
                if (value.Left(4).ToLower() == "tcp:" && value.Right(stem.Length).ToLower() == stem)
                {
                    server = value.Substring(4, value.Length - stem.Length - 4);
                    type = "Azure";
                }
                else
                {
                    server = value;
                    type   = "MySQL";
                }
            } 
        }
        private string server = null;

        /// Server Type
        public string Type { 
            get { return type; }
            set
            {
                if (value == "MySQL" || value == "Azure") type = value;
                else throw new Exception("Error setting Connection.Type. Value given was " + value + " - only 'MySQL' or 'Azure' are permitted.");
            }
        }
        private string type = null;

        /// Database Name 
        public string Database { get; set; }

        /// UserId 
        public string UserId   { get; set; }

        /// Password
        public string Password { get; set; }

        /// The Connection String 
        public string ConnectionString
        {
            get { return FormatConnect(); }
        }

        private void ParseConnect(string connect)
        {
            if (connect == null) return;

            string[] segs = connect.Split(';');
            foreach (string seg in segs)
            {
                int i = seg.IndexOf('=');
                if (i > 0)
                {
                    string key = seg.Substring(0, i).ToLower();
                    string value = seg.Substring(i + 1);
                    if (key == "data source"     || key == "server")   Server   = value;
                    if (key == "initial catalog" || key == "database") Database = value;
                    if (key == "user id" || key == "user" || key == "uid")      UserId   = value;
                    if (key == "password"        || key == "pwd")      Password = value;
                }
            }
            if (Server == null || Type == null || Database == null || UserId == null || Password == null)
                throw new Exception("Connection string " + connect + " is not formatted correctly.");
        }

        private string FormatConnect(bool forceDefault = false)
        {
            string database = Database ?? DefaultDB;
            if (database.ToLower() == "all" || forceDefault) database = DefaultDB;
            
            if (Type == "MySQL")
            {
                string others = ";port=3307;";
                return "Server=" + Server + ";Database=" + database + ";user=" + UserId + ";password=" + Password + others;
            }
            else
            {
                string server = "tcp:" + Server + ".database.windows.net,1433";
                string others = "Persist Security Info=False;MultipleActiveResultSets=False;Encrypt=True;";
                others += "TrustServerCertificate=False;Connection Timeout=30;";
                return "Server=" + server + ";Initial Catalog=" + database + ";User ID=" + UserId + ";Password=" + Password + ";" + others;
            }
        }
 
        /// Returns name of default database
        public string DefaultDB
        {
            get
            {
                return (Type == "MySQL") ? "mysql" : "Master";
            }
        }

        /// Get an MS SQL or MySQL Connection
        public IDbConnection GetConnection(bool forceDefault = false)
        {
            if (Type == "MySQL") return new MySqlConnection(FormatConnect(forceDefault));
            else                 return new SqlConnection(FormatConnect(forceDefault));
        }
    }
}
