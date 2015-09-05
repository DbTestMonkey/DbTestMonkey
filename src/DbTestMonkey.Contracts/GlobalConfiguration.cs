namespace DbTestMonkey.Contracts
{
   using System;
   using System.Collections.Generic;
   using System.ComponentModel;
   using System.Configuration;
   using System.Linq;
   using System.Text;
   using System.Threading.Tasks;

   public class GlobalConfiguration : ConfigurationSection
   {
      [ConfigurationProperty("defaultDbProviderType", IsRequired = false)]
      public string DefaultDbProvider 
      { 
         get
         {
            return (string)this["defaultDbProviderType"];
         }
      }

      public Type DefaultDbProviderType
      {
         get
         {
            return Type.GetType(DefaultDbProvider);
         }
      }

      [ConfigurationProperty("useParallelInitialisation", IsRequired = false, DefaultValue = true)]
      public bool UseParallelInitialisation
      {
         get
         {
            return (bool)this["useParallelInitialisation"];
         }
      }

      [ConfigurationProperty("deployDatabasesEachClass", IsRequired = false, DefaultValue = false)]
      public bool DeployDatabasesEachClass
      {
         get
         {
            return (bool)this["deployDatabasesEachClass"];
         }
      }
   }
}
