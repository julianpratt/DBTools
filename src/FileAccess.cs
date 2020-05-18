using System;
using System.IO;
using Mistware.Utils;


namespace DBTools
{
    /// StreamReader wrapper. Uses ContentRoot, WebRoot, BaseDirectory and PhysicalApplicationPath from Config to locate the file.
    public class FileAccess
    {
        private StreamReader Reader { set; get; }

        /// Open file for reading. Uses ContentRoot, WebRoot, BaseDirectory and PhysicalApplicationPath from Config to locate the file.
         /// <param name="filename">The name of the file to open.</param>
        public FileAccess(string filename)
        {
            Reader = null;

            string fullname = null;
            if (File.Exists(filename)) 
            {
                fullname = filename;
            }
            else
            {
                fullname                       = CheckExists("BaseDirectory",           filename);
                if (fullname == null) fullname = CheckExists("PhysicalApplicationPath", filename);
                if (fullname == null) fullname = CheckExists("ContentRoot",             filename);
                if (fullname == null) fullname = CheckExists("WebRoot",                 filename);
            }
            if (fullname == null)
            {
                Log.Me.Error("In FileAccess ctor: Cannot find " + filename);    
            }
            else
            {
                // Found file
                Reader = File.OpenText(fullname);
            }
        }

        private string CheckExists(string pathname, string filename)
        {
            string path = Config.Get(pathname);
            if (path == null) return null;
            string fullname = path + filename;
            if (File.Exists(fullname)) return fullname;
            else                       return null; 
        }

        /// Reads a line of characters from the current file and returns the data as a string.
        /// <returns>The next line from the input file, or null if the end of the input file is reached.</returns>
        /// <exception cref="OutOfMemoryException">There is insufficient memory to allocate a buffer for the returned string.</exception>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public string ReadLine()
        {
            return (Reader != null) ? Reader.ReadLine() : null;
        }

        /// Reads all characters from the current position to the end of the file. 
        /// <returns>The rest of the file as a string, from the current position to the end. If the current position is at the end of the file, returns an empty string ("").</returns>
        /// <exception cref="OutOfMemoryException">There is insufficient memory to allocate a buffer for the returned string.</exception>
        /// <exception cref="IOException">An I/O error occurs.</exception>
        public string ReadToEnd()
        {
            return (Reader != null) ? Reader.ReadToEnd() : null;
        }

        /// Close a stream. In fact it just does a Dispose().
        /// Perhaps this method should have been named Dispose()   
        public void Close()
        {
            if (Reader != null) Reader.Dispose();
            Reader = null;
        }

        /// Check whether a stream is still open (i.e. hasn't been closed).
        public bool IsOpen()
        {
            return (Reader != null); 
        }

        /// Tests whether stream has any more characters to read. False if more to read. True if at end.
        public bool EndOfStream { get { return Reader != null ? (Reader.Peek() == -1) : true; } }

    }
}