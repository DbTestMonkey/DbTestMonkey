namespace DbTestMonkey.Contracts
{
   using System;

   [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false)]
   public class ConnectionAttribute : Attribute
   {
      public ConnectionAttribute()
      {
      }

      public ConnectionAttribute(string targetDatabaseName)
      {
         TargetDatabaseName = targetDatabaseName;
      }

      public string TargetDatabaseName { get; private set; }
   }
}