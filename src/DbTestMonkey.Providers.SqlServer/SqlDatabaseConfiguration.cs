namespace DbTestMonkey.Providers.SqlServer
{
   using System.Configuration;
   using Contracts;

   public class SqlDatabaseConfiguration : DatabaseConfiguration
   {
      [ConfigurationProperty("dacpacFilePath", IsRequired = true)]
      public string DacPacFilePath
      {
         get
         {
            return (string)this["dacpacFilePath"];
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