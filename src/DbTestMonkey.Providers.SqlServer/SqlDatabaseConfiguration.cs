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

      [ConfigurationProperty("executePostDeploymentScriptPerTest", IsRequired = false, DefaultValue = true)]
      public bool ExecutePostDeploymentScriptPerTest
      {
         get
         {
            return (bool)this["executePostDeploymentScriptPerTest"];
         }
      }
   }
}