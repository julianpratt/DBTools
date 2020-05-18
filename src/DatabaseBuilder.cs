using System;
using System.Collections.Generic;
using System.Text;
using Mistware.Utils;

namespace DBTools
{
	/// Class to convert database definitions to SQL create statements.
	/// This needs to be merged with the code in Attributes, because there is considerable overlap, 
	/// and it would be handy to be able to create databases from decorated POCOs. 
	public class Builder
  	{
		/// Database Name
    	public  string          DatabaseName    { get; set; } 

		/// List of Table definitions 
    	public  List<TableDefn> Tables          { get; set; } 

    	private TableDefn       table           { get; set; }	

		/// Returns a loaded database definition
		/// <param name="path">The full name of the folder containing database definitions.</param> 
		/// <param name="database">The name of the database, whose definition we are loading.</param> 
		public static Builder Load(string path, string database)
		{
			string definition = path + Tools.PathDelimiter + database + ".dbd";
            if (!System.IO.File.Exists(definition)) return null;

			return Load(definition);
		}

		/// Returns a loaded database definition
		/// <param name="definition">The filename of the file containing the database definition.</param> 
		public static Builder Load(string definition)
		{
			Builder d = new Builder();
			d.LoadDefinition(definition);
			return d;
		}

		/// Returns the SQL to create a database from its definition
		/// <param name="dbtype">Database Type. Is "MSSQL" or "MySQL" (the default).</param> 
		public string GetSQL(string dbtype)
		{
        	if (dbtype.ToUpper() == "AZURE") return this.WriteMSSQL();
	    	else                             return this.WriteMySQL();
		}

    	private void LoadDefinition(string filename)
    	{
      		int state = 0;

			FileAccess handle = new FileAccess(filename);
			if (!handle.IsOpen()) return;

			while (!handle.EndOfStream) 
			{
				string s = handle.ReadLine();
				List<string> words = s.Wordise();
				if (words.Count > 0)  // Skip blank lines
				{
					if (state == 0) // The Database name must be the first thing we see 
					{
						if (words[0].ToUpper() == "DATABASE")
						{
							if (words.Count == 2)
							{
								DatabaseName = words[1];
								Tables = new List<TableDefn>();
								state = 1;
							}
							else
							{
								Log.Me.Error("DATABASE statement in " + filename + " either missing database name or has too many values. Statement is: "+s);
								return;	
							}
						}
						else
						{
							Log.Me.Error("Missing DATABASE statement in first line of " + filename);
							return;		
						}

					}
					else if (state == 1) // Looking for the start of a Table
					{
						if (words[0].ToUpper() == "TABLE")
						{
							if (words.Count == 2)
							{
								table = new TableDefn();
								table.TableName = words[1];
								table.Columns = new List<ColumnDefn>();
								Tables.Add(table);
								state = 2;
							}
							else
							{
								Log.Me.Error("TABLE statement in " + filename + " either missing table name or has too many values. Statement is: "+s);
							  	return;	
							}
						}
						else
						{
							Log.Me.Error("Missing TABLE statement in " + filename);
							return;		
						}
					} 
					else // The only other state is 2, which is looking for a column definition or END  
					{
						if (words.Count == 1)
						{
							if (words[0].ToUpper() == "END")
							{
								// End of one table definition, set state to look for the next.
								state = 1;
							}
							else
							{
								Log.Me.Error("Table definition entry with just one word, and it isn't END, in " + filename + ". Statement is: "+s);
							  	return;	
							}
						}	
						else // Must have at least 2 entries which will be parsed as column name and type
						{
							ColumnDefn c = new ColumnDefn();
							c.ColumnName = words[0];
							c.ColumnType = words[1];
							c.IsIdentity = false;
							c.IsNullable = true;
							c.IsIndex    = false;
							c.FKey       = null;
							bool not     = false;
							int i = 2;
							while (i < words.Count)
							{
								// Parse optional modifiers
                				if      (words[i].ToUpper() == "IDENTITY") { c.IsIdentity = true; c.IsNullable = false; } 
                				else if (words[i].ToUpper() == "INDEX")    c.IsIndex = true;
                				else if (words[i].ToUpper() == "NOT")      not       = true;
                				else if (words[i].ToUpper() == "NULL") 
                				{
                					if (not) c.IsNullable = false;
                					else     c.IsNullable = true;
                  					not = false;
                				}   
                				else if (words[i].ToUpper() == "FKEY") 
                				{
                					if (i == words.Count)
                					{
                						Log.Me.Error("Missing foreign key after FKEY in "+filename+". Statement is: "+s);
								    	return;
                					}
                					else
                					{
                						++i;
                						c.FKey=words[i];
                					}
                				}  
                				else
                				{
                					Log.Me.Error("Illegal column modifier " + words[i] + " in "+filename+". Statement is: "+s);
								  	return;
                				}  
								++i;
							}
							// Check for conflicts
							if (c.IsIdentity && c.IsIndex) 
							{
								Log.Me.Error("Identity column cannot also be an Index in "+filename+". Statement is: "+s);
								return;
							}
							if (c.IsIdentity && c.IsNullable) 
							{
								Log.Me.Error("Identity column cannot be nullable in "+filename+". Statement is: "+s);
								return;
							}
							if (c.IsIdentity && c.FKey != null) 
							{
								Log.Me.Error("Identity column cannot also be a foreign key in "+filename+". Statement is: "+s);
								return;
							}
							if (not) 
							{
								Log.Me.Error("NOT on its own without NULL in "+filename+". Statement is: "+s);
								return;
							}
							table.Columns.Add(c);
						}
					}	
				}
			}
			handle.Close();
		}

    	private string WriteMySQL()
    	{

    		StringBuilder sb = new StringBuilder();
    		sb.Append("USE " + DatabaseName + ";\n\n");
    		foreach (TableDefn t in Tables)
    		{
    			sb.Append("DROP TABLE IF EXISTS " + t.TableName + ";\n\n");

    			sb.Append("CREATE TABLE " + t.TableName + " (\n");
    			string IdColumn = null;
    			string line = null;
    			foreach (ColumnDefn c in t.Columns)
    			{
					if (c.ColumnType.ToUpper() != "BLOB")
					{
    					if (line != null) sb.Append(line + ",\n");
    					line = c.ColumnName + " " + c.ColumnType;
    					if (c.IsNullable) line += " NULL";
          				else              line += " NOT NULL";   
    					if (c.IsIdentity) { line += " AUTO_INCREMENT"; IdColumn = c.ColumnName; }
					}	
    			}
    			// Primary Key Constraint
    			if (IdColumn != null)
    			{
    				if (line != null) sb.Append(line + ",\n");
    			 	line = "CONSTRAINT PK_" + t.TableName + " PRIMARY KEY (" + IdColumn + ")";
    			}
    			// Foreign Key Constraints
    			foreach (ColumnDefn c in t.Columns)
    			{
    				if (c.FKey != null) 
    				{ 
    					if (line != null) sb.Append(line + ",\n");
    					line = "CONSTRAINT FK_" + t.TableName + "_" + c.ColumnName + " FOREIGN KEY (" + c.ColumnName + ") REFERENCES " + c.FKey; 
    				}
    			}
    			if (line != null) sb.Append(line + ");\n");
    			sb.Append("\n");
    			int n=0;
   		  		foreach (ColumnDefn c in t.Columns)
    			{
    				if (c.IsIndex) 
    				{ 
    					sb.Append("CREATE INDEX IX_" + t.TableName + "_" +  c.ColumnName + " ON " + t.TableName + "(" + c.ColumnName + ");\n"); 
    					++n; 
    				}
    			}
    			if (n > 0) sb.Append("\n");
    		}
    		return sb.ToString();
    	}

    	private string WriteMSSQL()
    	{
    		StringBuilder sb = new StringBuilder();

			sb.Append("USE " + DatabaseName + ";\n\n");
      	
    		foreach (TableDefn t in Tables)
    		{  		
    			sb.Append("CREATE TABLE [dbo].[" + t.TableName + "](\n");
    			string IdColumn = null;
    			string line = null;
    			foreach (ColumnDefn c in t.Columns)
    			{
					if (c.ColumnType.ToUpper() != "BLOB")
					{
						if (line != null) sb.Append(line + ",\n");
    					line = c.ColumnName + " " + c.ColumnType;
    					if (c.IsNullable) line += " NULL";
          				else              line += " NOT NULL";   
    					if (c.IsIdentity) { line += " IDENTITY(1,1)"; IdColumn = c.ColumnName; }
					}
    			}
    			// Primary Key Constraint
    			if (IdColumn != null)
    			{
    				if (line != null) sb.Append(line + ",\n");
    				sb.Append("CONSTRAINT [PK_" + t.TableName + "] PRIMARY KEY CLUSTERED ( [" + IdColumn + "] ASC )\n");
    				sb.Append("    WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF,\n"); 
           			sb.Append("    IGNORE_DUP_KEY = OFF, ALLOW_ROW_LOCKS  = ON, ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY]\n");
    			}
    			else sb.Append(line + "\n");
        		sb.Append(") ON [PRIMARY];\n");

    			// Foreign Key Constraints
    			foreach (ColumnDefn c in t.Columns)
    			{
    				if (c.FKey != null) 
    				{ 
    					sb.Append("ALTER TABLE [dbo].[" + t.TableName + "] WITH CHECK\n");
    					sb.Append("ADD CONSTRAINT [FK_" + t.TableName + "_" + c.ColumnName + "]\n");
    					sb.Append("FOREIGN KEY ([" + c.ColumnName + "]) REFERENCES " + c.FKey + ";\n"); 

            			sb.Append("ALTER TABLE [dbo].[" + t.TableName + "] CHECK \n");
    					sb.Append("CONSTRAINT [FK_" + t.TableName + "_" + c.ColumnName + "];\n");
    				}
    			}

        		// Indexes
    			int n=0;
   		  		foreach (ColumnDefn c in t.Columns)
    			{
    				if (c.IsIndex) 
    				{ 
    					sb.Append("CREATE CLUSTERED INDEX [IX_" +  t.TableName + "_" + c.ColumnName + "]\n");
    					sb.Append("       ON [dbo].[" + t.TableName + "](" + c.ColumnName + " ASC)\n"); 
            			sb.Append("       WITH (PAD_INDEX  = OFF, STATISTICS_NORECOMPUTE  = OFF, SORT_IN_TEMPDB = OFF,\n");
            			sb.Append("       IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS  = ON, \n");
            			sb.Append("       ALLOW_PAGE_LOCKS  = ON) ON [PRIMARY];\n");
    					++n; 
    				}
    			}
    			if (n > 0) sb.Append("\n");

    		}	

	    	return sb.ToString();
    	} 

		/// Find a table, given it name (which may be pluralied).  
		public TableDefn FindTable(string name)
		{
			foreach(TableDefn t in this.Tables) 
			{
				if (t.TableName.ToLower()==name.ToLower()) return t;
				if (Pluralizer.Pluralize(t.TableName).ToLower()==name.ToLower()) return t;
			}
			return null;
		}

		/// Return the length of the longest table name
		public int MaxTableNameLength()
		{
			int n = 0;
			foreach(TableDefn t in Tables) if (t.TableName.Length > n) n = t.TableName.Length;
			return n;
		}

    }

	/// Table Definition. Consists of: Table name and list of Column Definitions. 
  	public class TableDefn
  	{
		/// The Table Name  
    	public string           TableName       { get; set; }

		/// List of Column Definitions. 
    	public List<ColumnDefn> Columns         { get; set; }	

		/// Find a column
		public ColumnDefn FindColumn(string name)
		{
			foreach(ColumnDefn c in this.Columns) 
			{
				if (c.ColumnName.ToLower()==name.ToLower()) return c;
			}
			return null;
		}

		/// Returns true if table has an identity column
		public bool HasIdentityColumn
		{
			get
			{
				foreach (ColumnDefn c in Columns) if (c.IsIdentity) return true;
				return false;
			}
		}

		/// Select Columns
		public string SelectCols()
		{
			string s = "";
			foreach (ColumnDefn c in Columns)
			{
				if (c.ColumnType.ToUpper() != "BLOB")
				{
					s += (s.Length > 0) ? ", " + c.ColumnName : c.ColumnName;
				}				
			}
			return s;
		}
  	}  

	/// Column Definition. Consists of: 
  	public class ColumnDefn
  	{
		/// Column Name
  		public string           ColumnName      { get; set; }

		/// Column Type - the SQL column type, e.g. varchar(100) 
  		public string           ColumnType      { get; set; }

		/// True is this is the identity column
    	public bool             IsIdentity      { get; set; }

		/// True if column is nullable
   		public bool             IsNullable      { get; set; }

		/// True if column is indexed
  		public bool             IsIndex         { get; set; }

		/// The name of the Foreign Key 
  		public string           FKey            { get; set; }

		/// Get the CLR Type of the column
		/// INT => int, BIT => bool, DATETIME => DateTime, FLOAT => Single and everything else is a string
		public Type CLRType()
		{
			if      (ColumnType.ToUpper()         == "INT")      return typeof(int);
			else if (ColumnType.ToUpper()         == "BIT")      return typeof(bool);
			else if (ColumnType.ToUpper()         == "BLOB")     return typeof(byte);
			else if (ColumnType.ToUpper()         == "DATETIME") return typeof(DateTime);
			else if (ColumnType.ToUpper()         == "FLOAT")    return typeof(Single);
			else if (ColumnType.ToUpper().Left(7) == "DECIMAL")  return typeof(decimal);
			else                                                 return typeof(string);
		}  
	}
}