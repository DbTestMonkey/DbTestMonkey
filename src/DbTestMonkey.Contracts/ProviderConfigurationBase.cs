namespace DbTestMonkey.Contracts
{
   using System.Configuration;

   public class ProviderConfigurationBase : ConfigurationSection
   {
      [ConfigurationProperty("databases")]
      [ConfigurationCollection(typeof(DatabaseConfiguration), AddItemName = "database")]
      public DatabasesConfigurationCollection Databases
      {
         get
         {
            return (DatabasesConfigurationCollection)this["databases"];
         }
      }
   }
}