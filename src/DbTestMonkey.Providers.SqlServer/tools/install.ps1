param($installPath, $toolsPath, $package, $project)

function PopulateAppConfigWithDummyData($project)
{
   Write-Host "Scanning app.config and merging in sample data."

   $xml = New-Object xml

   # Find the App.config file and create it if it doesn't yet exist.
   $config = $project.ProjectItems | where {$_.Name -eq "App.config"}

   if ($config -eq $null)
   {
      $appConfigPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($project.FullName), "App.config")
      $xml.Save($appConfigPath)
   }

   $config = $project.ProjectItems | where {$_.Name -eq "App.config"}

   # Find the absolute path.
   $localPath = $config.Properties | where {$_.Name -eq "LocalPath"}

   $xml.Load($localPath.Value)

   $configurationNode = $xml.SelectSingleNode("configuration")

   # Add the configSections node.
   $configSectionsNode = $xml.SelectSingleNode("configuration/configSections")

   if ($configSectionsNode -eq $null)
   {
      $configSectionsNode = $configurationNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "configSections", $null))
   }

   # Add the sectionGroup node.
   $dbTestMonkeySectionGroupNode = $xml.SelectSingleNode("configuration/configSections/sectionGroup[@name='dbTestMonkey']")

   if ($dbTestMonkeySectionGroupNode -eq $null)
   {
      $dbTestMonkeySectionGroupNode = $configSectionsNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "sectionGroup", $null))

      $attrib = MakeXmlAttribute $xml "name" "dbTestMonkey"
      $dbTestMonkeySectionGroupNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "type" "System.Configuration.ConfigurationSectionGroup, System.Configuration"
      $dbTestMonkeySectionGroupNode.Attributes.Append($attrib) > $null
   }

   # Add the global section node.
   $sectionNode = $xml.SelectSingleNode("configuration/configSections/sectionGroup[@name='dbTestMonkey']/section[@name='global']")

   if ($sectionNode -eq $null)
   {
      $sectionNode = $dbTestMonkeySectionGroupNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "section", $null))

      $attrib = MakeXmlAttribute $xml "name" "global"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "type" "DbTestMonkey.Contracts.GlobalConfiguration, DbTestMonkey.Contracts"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "allowLocation" "true"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "allowDefinition" "Everywhere"
      $sectionNode.Attributes.Append($attrib) > $null
   }

   # Add the database section node.
   $sectionNode = $xml.SelectSingleNode("configuration/configSections/sectionGroup[@name='dbTestMonkey']/section[@name='database']")

   if ($sectionNode -eq $null)
   {
      $sectionNode = $dbTestMonkeySectionGroupNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "section", $null))

      $attrib = MakeXmlAttribute $xml "name" "database"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "type" "DbTestMonkey.Contracts.DatabaseConfiguration, DbTestMonkey.Contracts"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "allowLocation" "true"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "allowDefinition" "Everywhere"
      $sectionNode.Attributes.Append($attrib) > $null
   }

   # Add the sqlServer section node.
   $sectionNode = $xml.SelectSingleNode("configuration/configSections/sectionGroup[@name='dbTestMonkey']/section[@name='sqlServer']")

   if ($sectionNode -eq $null)
   {
      $sectionNode = $dbTestMonkeySectionGroupNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "section", $null))

      $attrib = MakeXmlAttribute $xml "name" "sqlServer"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "type" "DbTestMonkey.Providers.SqlServer.ProviderConfiguration, DbTestMonkey.Providers.SqlServer"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "allowLocation" "true"
      $sectionNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "allowDefinition" "Everywhere"
      $sectionNode.Attributes.Append($attrib) > $null
   }

   # Add the dbTestMonkey node.
   $dbTestMonkeyNode = $xml.SelectSingleNode("configuration/dbTestMonkey")

   if ($dbTestMonkeyNode -eq $null)
   {
      $dbTestMonkeyNode = $configurationNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "dbTestMonkey", $null))
   }

   # Add the global node.
   $globalNode = $xml.SelectSingleNode("configuration/dbTestMonkey/global")

   if ($globalNode -eq $null)
   {
      $globalNode = $dbTestMonkeyNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "global", $null))
   }

   # Add the defaultDbProviderType attribute to the global node.
   if ($globalNode.Attributes["defaultDbProviderType"] -eq $null)
   {
      $attrib = MakeXmlAttribute $xml "defaultDbProviderType" "DbTestMonkey.Providers.SqlServerProvider, DbTestMonkey.Providers.SqlServer"
      $globalNode.Attributes.Append($attrib) > $null
   }

   # Add the sqlServer node.
   $sqlServerNode = $xml.SelectSingleNode("configuration/dbTestMonkey/sqlServer")

   if ($sqlServerNode -eq $null)
   {
      $sqlServerNode = $dbTestMonkeyNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "sqlServer", $null))

      $attrib = MakeXmlAttribute $xml "isLocalDbInstance" "true"
      $sqlServerNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "localDbInstanceName" "YourLocalDbInstanceName"
      $sqlServerNode.Attributes.Append($attrib) > $null

      # Add the databases node.
      $databasesNode = $sqlServerNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "databases", $null))

      # Add the database node.
      $databaseNode = $databasesNode.AppendChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "database", $null))

      $attrib = MakeXmlAttribute $xml "databaseName" "YourDatabaseName"
      $databaseNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "connectionPropertyName" "YourDatabaseConnectionPropertyName"
      $databaseNode.Attributes.Append($attrib) > $null

      $attrib = MakeXmlAttribute $xml "dacpacFilePath" "..\..\..\DACPACs\YourDatabaseName.dacpac"
      $databaseNode.Attributes.Append($attrib) > $null
   }

   # Save the App.config file
   $xml.Save($localPath.Value)
}

function MakeXmlAttribute($xml, $key, $value)
{
   $attrib = $xml.CreateAttribute($key)
   $attrib.Value = $value

   return $attrib
}

PopulateAppConfigWithDummyData $project