namespace DbTestMonkey.Providers.SqlServer
{
   using System.Configuration;
   using Contracts;

   public class SqlDatabaseConfiguration : DatabaseConfiguration
   {
      [ConfigurationProperty("dacPacFilePath", IsRequired = true)]
      public string DacPacFilePath
      {
         get
         {
            return (string)this["dacPacFilePath"];
         }
      }
   }
}