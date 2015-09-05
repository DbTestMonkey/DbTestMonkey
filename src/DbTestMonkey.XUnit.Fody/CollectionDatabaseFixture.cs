namespace DbTestMonkey.XUnit.Fody
{
   using System;
   using System.Configuration;
   using Contracts;

   /// <summary>
   /// A fixture used by XUnit facts and theories when database are involved and set up
   /// on a per collection basis.
   /// </summary>
   public class CollectionDatabaseFixture
   {
      /// <summary>
      /// The path to use when looking for global DbTestMonkey configuration.
      /// </summary>
      private const string SectionXPath = "dbTestMonkey/global";

      /// <summary>
      /// The type of class that first ran a test in the current assembly. This is purely
      /// used to ensure database set up and teardown does not occur more than once per
      /// assembly if configured as such.
      /// </summary>
      private Type _type;

      /// <summary>
      /// Initializes a new instance of the <see cref="CollectionDatabaseFixture"/> class.
      /// </summary>
      public CollectionDatabaseFixture()
      {
         _type = null;
      }

      /// <summary>
      /// Gets or sets the type of class that first ran a test in the current assembly. This is purely
      /// used to ensure database set up and teardown does not occur more than once per
      /// assembly if configured as such.
      /// </summary>
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

               GlobalConfiguration globalConfig =
                  (GlobalConfiguration)ConfigurationManager.GetSection(SectionXPath);

               // If not each class, then each collection.
               if (!globalConfig.DeployDatabasesEachClass)
               {
                  DbController.BeforeTestGroup(_type, LogAction);
               }
            }
         }
      }

      /// <summary>
      /// Gets or sets the action to call when diagnostic logs should be output.
      /// </summary>
      public Action<string> LogAction { get; set; }

      /// <summary>
      /// Triggers the AfterTestGroup method to dispose or clean up any resources
      /// that were held for the length of the collection test run.
      /// </summary>
      public void Dispose()
      {
         GlobalConfiguration globalConfig =
            (GlobalConfiguration)ConfigurationManager.GetSection(SectionXPath);

         // If not each class, then each collection.
         if (!globalConfig.DeployDatabasesEachClass)
         {
            DbController.AfterTestGroup(_type, LogAction);
         }
      }
   }
}
