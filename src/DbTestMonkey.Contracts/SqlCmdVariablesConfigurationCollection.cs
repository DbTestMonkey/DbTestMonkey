using System.Configuration;

namespace DbTestMonkey.Contracts
{
   public class SqlCmdVariablesConfigurationCollection : ConfigurationElementCollection
   {
	   public SqlCmdVariableConfiguration this[int index]
	   {
         get
         {
            return base.BaseGet(index) as SqlCmdVariableConfiguration;
         }

         set
         {
            if (base.BaseGet(index) != null)
            {
               this.BaseRemoveAt(index);
            }

            this.BaseAdd(index, value);
         }
	   }

	   public new SqlCmdVariableConfiguration this[string responseString]
	   {
         get
         {
            return (SqlCmdVariableConfiguration)BaseGet(responseString);
         }

         set
         {
            if (BaseGet(responseString) != null)
            {
               BaseRemoveAt(BaseIndexOf(BaseGet(responseString)));
            }

            BaseAdd(value);
         }
	   }

	   protected override ConfigurationElement CreateNewElement()
	   {
         return new SqlCmdVariableConfiguration();
	   }

	   protected override object GetElementKey(ConfigurationElement element)
	   {
         return ((SqlCmdVariableConfiguration)element).Name;
	   }
   }
}