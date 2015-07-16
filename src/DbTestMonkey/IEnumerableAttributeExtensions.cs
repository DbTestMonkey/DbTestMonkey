namespace DbTestMonkey
{
   using System;
   using System.Collections.Generic;
   using System.Linq;
   using DbTestMonkey.Contracts;

   internal static class IEnumerableAttributeExtensions
   {
      public static bool RequiresConnectionToDatabase(
         this IEnumerable<Attribute> customAttributes,
         string databaseName)
      {
         Attribute connectionAttribute = customAttributes
            .Where(a => a.GetType().IsAssignableFrom(typeof(ConnectionAttribute)))
            .FirstOrDefault();

         if (connectionAttribute != null)
         {
            if (((ConnectionAttribute)connectionAttribute).TargetDatabaseName == databaseName)
            {
               return true;
            }
         }

         return false;
      }
   }
}