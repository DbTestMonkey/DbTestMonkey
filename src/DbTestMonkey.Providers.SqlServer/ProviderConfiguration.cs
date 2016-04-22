namespace DbTestMonkey.Providers.SqlServer
{
   using System.Configuration;
   using DbTestMonkey.Contracts;

   public class ProviderConfiguration : ProviderConfigurationBase
   {
      [ConfigurationProperty("isLocalDbInstance", IsRequired = true, DefaultValue = true)]
      public bool IsLocalDbInstance
      {
         get
         {
            return (bool)this["isLocalDbInstance"];
         }
      }

      [ConfigurationProperty("connectionString", IsRequired = false)]
      public string ConnectionString
      {
         get
         {
            return (string)this["connectionString"];
         }
      }

      [ConfigurationProperty("localDbInstanceName", IsRequired = false)]
      public string LocalDbInstanceName
      {
         get
         {
            return (string)this["localDbInstanceName"];
         }
      }

      [ConfigurationProperty("allowedLocalDbVersions", IsRequired = false)]
      [ConfigurationCollection(typeof(LocalDbAllowedVersion), AddItemName = "version")]
      public SqlLocalDbAllowedVersionsCollection Versions
      {
         get
         {
            return (SqlLocalDbAllowedVersionsCollection)this["allowedLocalDbVersions"];
         }
      }

      [ConfigurationProperty("databases")]
      [ConfigurationCollection(typeof(SqlDatabaseConfiguration), AddItemName = "database")]
      public new SqlDatabasesConfigurationCollection Databases
      {
         get
         {
            return (SqlDatabasesConfigurationCollection)this["databases"];
         }
      }
   }
}