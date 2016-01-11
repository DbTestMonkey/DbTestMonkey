namespace DbTestMonkey.XUnit.Fody
{
   using System;
   using System.Configuration;
   using Contracts;

   /// <summary>
   /// A fixture used by XUnit facts and theories when database are involved and set up
   /// on a per class basis.
   /// </summary>
   public class ClassDatabaseFixture : IDisposable
   {
      /// <summary>
      /// The path to use when looking for global DbTestMonkey configuration.
      /// </summary>
      private const string SectionXPath = "dbTestMonkey/global";

      /// <summary>
      /// The type of class that this instance of the fixture is attached to
      /// </summary>
      private Type _type;

      /// <summary>
      /// Initializes a new instance of the <see cref="ClassDatabaseFixture"/> class.
      /// </summary>
      public ClassDatabaseFixture()
      {
         _type = null;
      }

      /// <summary>
      /// Gets or sets the type of class that this instance of the fixture is attached to.
      /// If per-class deploys are configured then this will also set up the databases when
      /// the property is set for the first time.
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

               if (globalConfig.DeployDatabasesEachClass)
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
      /// that were held for the length of the test group.
      /// </summary>
      public void Dispose()
      {
         GlobalConfiguration globalConfig =
            (GlobalConfiguration)ConfigurationManager.GetSection(SectionXPath);

         if (globalConfig.DeployDatabasesEachClass)
         {
            DbController.AfterTestGroup(_type, LogAction);
         }
      }
   }
}
