param($installPath, $toolsPath, $package, $project)

$tempFodyWeaversFileName = "DbTestMonkeyDeleteMe.xml"
$finalFodyWeaversFileName = "FodyWeavers.xml"

function Update-FodyConfig($addinName, $project)
{
   Write-Host "Update-FodyConfig" 
   $tempFodyWeaversPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($project.FullName), $tempFodyWeaversFileName)
   $fodyWeaversPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($project.FullName), $finalFodyWeaversFileName)

   if (-not [System.IO.File]::Exists($fodyWeaversPath))
   {
      Write-Host "FodyWeavers.xml does not yet exist in the project. Ensuring it is now created."
      $project.ProjectItems.Item($tempFodyWeaversFileName).Name = $finalFodyWeaversFileName
      [System.IO.File]::Move($tempFodyWeaversPath, $fodyWeaversPath)
   }
   else
   {
      Write-Host "FodyWeavers.xml already exists in the project. Now deleting our temporary version."
      [System.IO.File]::Delete($tempFodyWeaversPath)
   }

   $project.ProjectItems.Item($tempFodyWeaversFileName).Delete()

   $xml = [xml](get-content $fodyWeaversPath)

   $weavers = $xml["Weavers"]
   $node = $weavers.SelectSingleNode($addinName)

   if (-not $node)
   {
      Write-Host "Appending node"
      $newNode = $xml.CreateElement($addinName)

      Write-Host "New Node $newNode"
      $weavers.AppendChild($newNode)
   }

   $xml.Save($fodyWeaversPath)
}

function Fix-ReferencesCopyLocal($package, $project)
{
   Write-Host "Fix-ReferencesCopyLocal $addinName"
   $asms = $package.AssemblyReferences | %{$_.Name}

   foreach ($reference in $project.Object.References)
   {
      if ($asms -contains $reference.Name + ".dll")
      {
         if($reference.CopyLocal -eq $false)
         {
            $reference.CopyLocal = $true;
         }
      }
   }
}

function UnlockWeaversXml($project)
{
   Write-Host "Unlocking WeaversXml"
   $fodyWeaversProjectItem = $project.ProjectItems.Item($finalFodyWeaversFileName);
   if ($fodyWeaversProjectItem)
   {
      $fodyWeaversProjectItem.Open("{7651A701-06E5-11D1-8EBD-00A0C90F26EA}")
      $fodyWeaversProjectItem.Save()
   }   
}

function PopulateAppConfigWithDummyData($project)
{
   Write-Host "Scanning app.config and merging in sample data."

   $xml = New-Object xml

   # Find the App.config file and create it if it doesn't yet exist.
   $config = $project.ProjectItems | where {$_.Name -eq "App.config"}

   if ($config -eq $null)
   {
      $appConfigPath = [System.IO.Path]::Combine([System.IO.Path]::GetDirectoryName($project.FullName), "App.config")

      $declaration = $xml.CreateXmlDeclaration("1.0",$null, $null)
      $declaration.Encoding = "UTF-8"
      $xml.AppendChild($declaration)

      $rootNode = $xml.CreateElement("configuration")
      $xml.AppendChild($rootNode)
      $xml.Save($appConfigPath)

      $project.ProjectItems.AddFromFile($appConfigPath)
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
      $configSectionsNode = $configurationNode.PrependChild($xml.CreateNode([System.Xml.XmlNodeType]::Element, "configSections", $null))
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

   # Save the App.config file
   $xml.Save($localPath.Value)
}

function MakeXmlAttribute($xml, $key, $value)
{
   $attrib = $xml.CreateAttribute($key)
   $attrib.Value = $value

   return $attrib
}

UnlockWeaversXml($project)

Update-FodyConfig $package.Id.Replace(".Fody", "") $project

Fix-ReferencesCopyLocal $package $project

PopulateAppConfigWithDummyData $project