namespace DbTestMonkey.Providers.SqlServer
{
   using System.Configuration;
   using DbTestMonkey.Contracts;

   public class SqlDatabasesConfigurationCollection : DatabasesConfigurationCollection
   {
      public new SqlDatabaseConfiguration this[int index]
      {
         get
         {
            return base.BaseGet(index) as SqlDatabaseConfiguration;
         }

         set
         {
            if (base.BaseGet(index) != null)
            {
               base.BaseRemoveAt(index);
            }

            this.BaseAdd(index, value);
         }
      }

      public new SqlDatabaseConfiguration this[string responseString]
      {
         get
         {
            return (SqlDatabaseConfiguration)BaseGet(responseString);
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
         return new SqlDatabaseConfiguration();
      }

      protected override object GetElementKey(ConfigurationElement element)
      {
         return ((SqlDatabaseConfiguration)element).DatabaseName;
      }
   }
}