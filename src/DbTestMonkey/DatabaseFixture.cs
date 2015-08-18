namespace DbTestMonkey
{
   using System;

   public class DatabaseFixture : IDisposable
   {
      private Type _type;

      public DatabaseFixture()
      {
         _type = null;
      }

      public Type ClassType 
      { 
         get
         {
            return _type;
         } 

         set
         {
            // We know this property is set at the beginning of the constructor
            // and this object lives for the life of the tests in the class.
            // Therefore we know that this BeforeTestGroup code will only execute
            // once per class.
            if (_type == null)
            {
               _type = value;
               DbController.BeforeTestGroup(_type, LogAction);
            }
         }
      }

      public Action<string> LogAction { get; set; }

      public void Dispose()
      {
         DbController.AfterTestGroup(_type, LogAction);
      }
   }
}
