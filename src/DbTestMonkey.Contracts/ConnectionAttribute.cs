using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DbTestMonkey.Contracts
{
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
