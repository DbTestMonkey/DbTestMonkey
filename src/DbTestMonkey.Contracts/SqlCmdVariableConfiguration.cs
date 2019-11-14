using System.Configuration;

namespace DbTestMonkey.Contracts
{
   public class SqlCmdVariableConfiguration : ConfigurationElement
   {
	   [ConfigurationProperty("name", IsRequired = true)]
	   public string Name
	   {
		   get
		   {
			   return (string)this["name"];
		   }
	   }

	   [ConfigurationProperty("value", IsRequired = true)]
	   public string Value
	   {
		   get
		   {
			   return (string)this["value"];
		   }
	   }
   }
}