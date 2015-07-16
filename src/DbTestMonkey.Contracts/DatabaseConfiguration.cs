namespace DbTestMonkey.Contracts
{
   using System;
   using System.Collections.Generic;
   using System.Configuration;
   using System.Linq;
   using System.Text;
   using System.Threading.Tasks;

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
   }
}
