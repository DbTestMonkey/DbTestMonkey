namespace DbTestMonkey.Providers.SqlServer
{
   using System.Configuration;

   public class SqlLocalDbAllowedVersionsCollection : ConfigurationElementCollection
   {
      public new SqlLocalDbAllowedVersionsCollection this[string responseString]
      {
         get
         {
            return (SqlLocalDbAllowedVersionsCollection)BaseGet(responseString);
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
         return new LocalDbAllowedVersion();
      }

      protected override object GetElementKey(ConfigurationElement element)
      {
         return ((LocalDbAllowedVersion)element).Version;
      }
   }
}
