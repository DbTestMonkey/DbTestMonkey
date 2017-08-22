namespace DbTestMonkey.Contracts
{
   using System.Configuration;

   public class DatabasesConfigurationCollection : ConfigurationElementCollection
   {
      public DatabaseConfiguration this[int index]
      {
         get
         {
            return base.BaseGet(index) as DatabaseConfiguration;
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

      public new DatabaseConfiguration this[string responseString]
      {
         get
         {
            return (DatabaseConfiguration)BaseGet(responseString);
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

      protected override System.Configuration.ConfigurationElement CreateNewElement()
      {
         return new DatabaseConfiguration();
      }

      protected override object GetElementKey(ConfigurationElement element)
      {
         return ((DatabaseConfiguration)element).DatabaseName;
      }
   }
}