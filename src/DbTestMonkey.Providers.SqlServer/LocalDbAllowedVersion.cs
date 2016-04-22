namespace DbTestMonkey.Providers.SqlServer
{
   using System.Configuration;
   using System.Xml;

   public class LocalDbAllowedVersion : ConfigurationElement
   {
      private string version;

      protected override void DeserializeElement(XmlReader reader, bool serializeCollectionKey)
      {
         version = (string)reader.ReadElementContentAs(typeof(string), null);
      }

      [ConfigurationProperty("version", IsRequired = true)]
      public string Version
      {
         get
         {
            return version;
         }
      }
   }
}
