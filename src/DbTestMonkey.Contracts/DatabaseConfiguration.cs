namespace DbTestMonkey.Contracts
{
   using System.Configuration;

   public class DatabaseConfiguration : ConfigurationElement
   {
      [ConfigurationProperty("databaseName", IsRequired = true)]
      public string DatabaseName
      {
         get
         {
            return (string)this["databaseName"];
         }
      }

      [ConfigurationProperty("connectionPropertyName", IsRequired = false)]
      public string ConnectionPropertyName
      {
         get
         {
            return (string)this["connectionPropertyName"];
         }
      }

      [ConfigurationProperty("SqlCommandVariables", IsRequired = false)]
      [ConfigurationCollection(typeof(SqlCmdVariableConfiguration), AddItemName = "SqlCommandVariable")]
      public SqlCmdVariablesConfigurationCollection SqlCommandVariables
      {
	      get
	      {
		      return (SqlCmdVariablesConfigurationCollection)this["SqlCommandVariables"];
	      }
      }
   }
}