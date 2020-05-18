using System;
using System.IO;
using System.Text;
using Mistware.Utils;


namespace DBTools
{
    /// Reads and writes backup files
    public class BackupFile
    {
        private string BackupPath { get; set; }
        private string Database   { get; set; }

        /// Open a backup file for reading or writing
        public BackupFile(string backupPath, string database)
        { 
            BackupPath = backupPath;
            Database   = database;
        }

        private string fileNameToday = null;

        /// The name of the backup being created. Property used to zip it up and delete it. 
        public string FileNameToday
        {
            get
            {
                if (fileNameToday == null)
                    fileNameToday = String.Format("{0}{1}{2}-{3:yyyy-MM-dd}.xml", BackupPath, 
                                Tools.PathDelimiter, Database, DateTime.Now);
                return fileNameToday;
            }
        }

        /// Restore uses the latest backup for restoration
        public string ArchiveLatest
        {
            get 
            { 
                if (archiveLatest == null)
                {
                    archiveLatest = FindLatestArchive(); 
                    if (archiveLatest != null) archiveLatest = BackupPath + Tools.PathDelimiter + archiveLatest;
                } 
                    
                return archiveLatest;
            }
        }
        private string archiveLatest = null;
    

        /// Change a filename's extension
        public string ChangeExt(string filename, string newext)
        {
            if (filename == null) return null;

            return filename.Left(filename.LastIndexOf('.')) + "." + newext; 
        }

        /// Ensure the backup file hasn't been left lying around by a previous failed process
        public void DeleteIfExists()
        {
             if (File.Exists(FileNameToday)) File.Delete(FileNameToday);
        }

        /// Add a line to the backup file (with new line)
        public void AppendLine(string text)
        {
            File.AppendAllText(FileNameToday, text, Encoding.UTF8);
            File.AppendAllText(FileNameToday, "\n", Encoding.UTF8);
        }

        private StreamReader rdr = null;

        /// Open the backup file as a Stream for reading during restore
        public Stream OpenStream(string backupFile)
        {
            if (rdr == null) rdr = File.OpenText(backupFile);
            else throw new Exception("BackupFile.OpenStream() called when already open.");

            return rdr.BaseStream;
        }

        /// Close an open backup file 
        public void Close()
        {
            if (rdr != null) rdr.Dispose();
            else throw new Exception("BackupFile.Close() called when not open.");
        }

        private string FindLatestArchive()
        {
            DateTime latest = new DateTime(2000,1,1);
            string result = null;
            
            DirectoryInfo di = new DirectoryInfo(BackupPath);

            foreach (FileInfo fi in di.EnumerateFiles(Database + "-*.zip"))
            {
                string name = fi.Name.ToLower();
                string[] parts = name.Left(name.LastIndexOf('.')).Split('-');
                int year  = parts[1].ToInteger();
                int month = parts[2].ToInteger();
                int day   = parts[3].ToInteger();

                DateTime found = new DateTime(year, month, day);

                if (found > latest && name == String.Format("{0}-{1:yyyy-MM-dd}.zip", Database.ToLower(), found) )
                {
                    latest = found;
                    result = fi.Name;
                } 
                
            }

            return result;
        }
    }
}