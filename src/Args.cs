using System;
using System.Collections.Generic;
using Mistware.Utils;

namespace DBTools
{
    /// Parse console app command line arguments. 
    /// The generic rules are:
    /// 1. The 1st argument is treated as a command
    /// 2. All subsequent arguments are parameters to that command
    /// 3. Until the first option (signified by a minus sign followed by a single character)
    /// 4. No parameters can follow options (for clarity)
    /// 5. Usually there is a space between the option character and its value, but this can be omitted  
    /// 6. Some options have no value    
    public class Args
    {
        /// ctor for Args class, parses the arguments 
        public Args(string[] args)
        {
            Parameters = new List<string>();
            Options    = new Dictionary<string,string>();
            Action     = null;

            if (args != null && args.Length > 0) Action = args[0].ToLower();
            bool foundOption = false; 

            for (int i = 1; i < args.Length; ++i)
            {
                string arg = args[i];
                string next = null;
                if ( i+1 < args.Length) next = args[i+1];

                if (arg[0] == '-')
                {
                    // Found an option, so process it.
                    foundOption = true;
                    if (arg.Length == 1) Log.Me.Error("Command line option character (-) found, but no option.");
                    else if (arg.Length == 2)
                    {
                        string option = arg.Mid(1,1);
                        if (next[0] == '-') Options.Add(option, null);
                        else 
                        {
                            Options.Add(option, next);
                            ++i;
                        }
                    }
                    else Options.Add(arg.Substring(1,1), arg.Substring(2));
                }
                else if (!foundOption) Parameters.Add(arg);
                else Log.Me.Error("Ambiguous command line parameter ignored: " + arg);
            }
        }

        /// Lower case action - from first argument
        public string                    Action     { get; private set;}

        /// Subsequent arguments until first option
        public List<string>              Parameters { get; private set; }

        /// Options are single characters preceeded by a minus sign and possibly followed by a value  
        public Dictionary<string,string> Options    { get; private set; }

        /// Get value from options
        public string LookupOptionValue(string option, string defaultValue)
        {
            string result = null;

            if (Options.ContainsKey(option)) 
            {
                result = Options[option];
                if (result == null) Log.Me.Error("Command line option '"+ option + "' had its value omitted.");
            }  
            if (result == null) result = defaultValue;          

            return result;
        } 

        /// Returns true if an option was set (otherwise false).
        /// This option must not have a value. 
        public bool LookupOption(string option)
        {
            if (Options.ContainsKey(option)) 
            {
                if (Options[option] != null) Log.Me.Error("Command line option '"+ option + "' was given an unneccessary value (" + Options[option] + ").");
                return true;
            }            
            else return false;
        } 
    }
}
