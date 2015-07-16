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
            // TODO: Make this private or something.
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
   }
}
