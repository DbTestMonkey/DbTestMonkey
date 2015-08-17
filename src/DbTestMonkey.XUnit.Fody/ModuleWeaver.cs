namespace DbTestMonkey.XUnit.Fody
{
   using System;
   using System.Collections.Generic;
   using System.IO;
   using System.Linq;
   using System.Reflection;
   using Mono.Cecil;
   using Mono.Cecil.Cil;
   using Mono.Cecil.Rocks;
   using DbTestMonkey;
   using Xunit;
   using Xunit.Abstractions;

   /// <summary>
   /// Fody class used for defining the weaving procedure for target assemblies.
   /// </summary>
   public class ModuleWeaver
   {
      /// <summary>
      /// Initializes a new instance of the ModuleWeaver class.
      /// </summary>
      public ModuleWeaver()
      {
         LogInfo = s => { };
         LogError = s => { };
      }

      /// <summary>
      /// Gets or sets a delegate for logging informational messages during weaving.
      /// </summary>
      public Action<string> LogInfo { get; set; }

      /// <summary>
      /// Gets or sets a delegate for logging error messages during weaving.
      /// </summary>
      public Action<string> LogError { get; set; }

      /// <summary>
      /// Gets or sets a delegate for logging warning messages during weaving.
      /// </summary>
      public Action<string> LogWarning { get; set; }

      /// <summary>
      /// Gets or sets an object containing context about the module currently being weaved.
      /// </summary>
      public ModuleDefinition ModuleDefinition { get; set; }

      /// <summary>
      /// Gets or sets an instance of Mono.Cecil.IAssemblyResolver for resolving assembly references.
      /// </summary>
      public IAssemblyResolver AssemblyResolver { get; set; }

      /// <summary>
      /// Core entry method called by Fody during MSBuild.
      /// </summary>
      public void Execute()
      {
         try
         {
            IEnumerable<TypeDefinition> types = ModuleDefinition.GetTypes();
            
            // Weave any classes that have a UsesDatabasesAttribute on it or one or more properties have a ConnectionAttribute
            // attribute on them.
            var matchingTypes = types.Where(t => 
               t.CustomAttributes.Any(a => a.AttributeType.Name == "UsesDatabasesAttribute") || 
               t.Properties.Any(p => p.CustomAttributes.Any(pa => pa.AttributeType.Name == "ConnectionAttribute")));

            foreach (var type in matchingTypes)
            {
               if (type.HasInterface("System.IDisposable"))
               {
                  LogInfo(type.Name + " already implements IDisposable.");
               }
               else
               {
                  LogInfo(type.Name + " does not implement IDisposable. Injecting code to cause implementation.");

                  EnsureTypeImplementsIDisposable(type);

                  LogInfo(type.Name + " now implements IDisposable.");
               }

               InjectTryFinallyWithAfterTestCall(type);
               LogInfo(type.Name + " has had database shutdown code injected into the Dispose method.");

               InjectDatabaseSetupInConstructor(type);
               LogInfo(type.Name + " has had database setup code injected into the Constructor method.");
            }
         }
         catch (Exception ex)
         {
            LogError(ex.Message);
         }
      }

      /// <summary>
      /// Injects code for initialising database state and connections prior to test execution.
      /// </summary>
      /// <param name="type">The type of object which is currently the weaving target.</param>
      private void InjectDatabaseSetupInConstructor(TypeDefinition type)
      {
         if (type.GetConstructors().Count() > 1)
         {
            throw new WeavingException(
               "Class " + type.FullName + " has multiple constructors which is not currently supported. Please remove additional constructors.");
         }

         foreach (var ctor in type.GetConstructors())
         {
            Instruction firstInstruction = ctor.Body.Instructions.FirstOrDefault(i => i.InstructionCallsConstructor());

            var firstInstructionAfterBaseCtorCall = 
               firstInstruction == null ? 
               firstInstruction.Next : 
               ctor.Body.Instructions.First();

            /*
             * Ensure class implements IClassFixture<DatabaseFixture>
             * */
            if (!type.Interfaces.Any(t =>
               t.HasGenericParameters && t.GenericParameters.Any(p =>
                  p.FullName == typeof(DatabaseFixture).FullName)))
            {
               // Resolve IClassFixture reference.
               TypeDefinition xunitClassFixture = null;
               var xunitAssemblyRef = type.Module.AssemblyReferences.FirstOrDefault(ar => ar.Name == "xunit.core");

               if (xunitAssemblyRef == null)
               {
                  var xunitAssemblyPath =
                     Path.Combine(Path.GetDirectoryName(ModuleDefinition.FullyQualifiedName), "xunit.core.dll");

                  if (File.Exists(xunitAssemblyPath))
                  {
                     xunitClassFixture =
                        AssemblyDefinition.ReadAssembly(xunitAssemblyPath).MainModule.GetType("Xunit.IClassFixture`1");
                  }
                  else
                  {
                     throw new WeavingException("A reference to Xunit.Core was required but was not found. Please add the Nuget package NUnit (v2.0).");
                  }
               }
               else
               {
                  xunitClassFixture = AssemblyResolver.Resolve(xunitAssemblyRef).MainModule.GetType("Xunit.IClassFixture`1");
               }

               // Resolve DatabaseFixture reference.
               TypeDefinition dbFixture = GetDatabasesFixtureDefinition(type);

               var genericClassFixture =
                  xunitClassFixture.MakeGenericInstanceType(type.Module.Import(dbFixture));

               type.Interfaces.Add(type.Module.Import(genericClassFixture));
            }

            if (!ctor.Parameters.Any(p => p.ParameterType.FullName == typeof(DatabaseFixture).FullName))
            {
               ctor.Parameters.Add(new ParameterDefinition(type.Module.Import(GetDatabasesFixtureDefinition(type))));
            }

            // Ensure the class constructor has a ITestHelperOutput parameter.
            if (!ctor.Parameters.Any(p => p.ParameterType.FullName == typeof(ITestOutputHelper).FullName))
            {
               ctor.Parameters.Add(new ParameterDefinition(type.Module.Import(GetTestOutputHelperDefinition(type))));
            }

            /*
             * dbFixture.LogAction = testOutputHelper.WriteLine;
             * */

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Ldarg_S,
                  ctor.Parameters.First(p => p.ParameterType.FullName == typeof(DatabaseFixture).FullName)));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Ldarg_S,
                  ctor.Parameters.First(p => p.ParameterType.FullName == typeof(ITestOutputHelper).FullName)));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(OpCodes.Dup));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Ldvirtftn,
                  ctor.Module.Import(GetTestOutputHelperDefinition(type).Methods.First(m => m.Name == "WriteLine" && m.Parameters.Count == 1))));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Newobj,
                  ctor.Module.Import(typeof(Action<string>).GetConstructors().First())));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Callvirt,
                  ctor.Module.Import(GetDatabasesFixtureDefinition(type).GetMethods().First(m => m.Name == "set_LogAction"))));

            /*
             * dbFixture.ClassType = base.GetType();
             * */

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Ldarg_S, 
                  ctor.Parameters.First(p => p.ParameterType.FullName == typeof(DatabaseFixture).FullName)));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(OpCodes.Ldtoken, type));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Call,
                  ctor.Module.Import(typeof(Type).GetMethod("GetTypeFromHandle"))));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Callvirt,
                  ctor.Module.Import(GetDatabasesFixtureDefinition(type).GetMethods().First(m => m.Name == "set_ClassType"))));

            /*
             * DbController.BeforeTest(this, MethodBase.GetCurrentMethod());
             * */

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(OpCodes.Ldarg_0));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Call,
                  ctor.Module.Import(typeof(MethodBase).GetMethod("GetCurrentMethod"))));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Call,
                  ctor.Module.Import(GetDbControllerDefinition(type).GetMethods().First(m => m.Name == "BeforeTest"))));
         }
      }

      /// <summary>
      /// Method verifies that the type implements IDisposable and emits
      /// IL to implement it if necessary.
      /// </summary>
      /// <param name="type">The type that needs to be checked and potentially modified.</param>
      private void EnsureTypeImplementsIDisposable(TypeDefinition type)
      {
         // Verify whether the class already implements IDisposable. If not then
         // implement it.
         if (!type.HasInterface("System.IDisposable"))
         {
            type.Interfaces.Add(type.Module.Import(typeof(IDisposable)));

            // Verify whether the type already has a public Dipose method. If not then add a shell.
            if (!type.HasDisposeMethod())
            {
               var disposeMethodAttribs =
                  Mono.Cecil.MethodAttributes.Public |
                  Mono.Cecil.MethodAttributes.Final |
                  Mono.Cecil.MethodAttributes.HideBySig |
                  Mono.Cecil.MethodAttributes.NewSlot |
                  Mono.Cecil.MethodAttributes.Virtual;

               var disposeDefinition =
                  new MethodDefinition("Dispose", disposeMethodAttribs, ModuleDefinition.TypeSystem.Void);

               // Empty method shell that only returns void.
               disposeDefinition.Body.Instructions.Add(Instruction.Create(OpCodes.Nop));
               disposeDefinition.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

               type.Methods.Add(disposeDefinition);
            }
         }
      }

      /// <summary>
      /// Wraps the contents of the Dispose method in a try/finally block where
      /// the AfterTest method is called inside the finally.
      /// </summary>
      /// <param name="type"></param>
      private void InjectTryFinallyWithAfterTestCall(TypeDefinition type)
      {
         MethodDefinition disposeMethod = type.GetDisposeMethod();

         var instructions = disposeMethod.Body.Instructions;

         var startTryInstruction = instructions.First();

         var finallyStartInstruction = Instruction.Create(OpCodes.Nop);
         instructions.InsertBefore(instructions.Last(), finallyStartInstruction);

         var finallyEndInstruction = instructions.Last();

         /*
          * try
          * {
          *    // Existing dispose code in here.
          * }
          * finally
          * {
          * }
          * */
         disposeMethod.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
         {
            TryStart = startTryInstruction,
            TryEnd = finallyStartInstruction,
            HandlerStart = finallyStartInstruction,
            HandlerEnd = finallyEndInstruction
         });

         disposeMethod.Body.Instructions.InsertBefore(finallyStartInstruction, Instruction.Create(OpCodes.Leave_S, finallyEndInstruction));

         /*
          * DbController.AfterTest(MethodBase.GetCurrentMethod());
          * */
         disposeMethod.Body.Instructions.InsertBefore(finallyEndInstruction, Instruction.Create(OpCodes.Call, disposeMethod.Module.Import(typeof(MethodBase).GetMethod("GetCurrentMethod"))));
         disposeMethod.Body.Instructions.InsertBefore(finallyEndInstruction, Instruction.Create(OpCodes.Call, disposeMethod.Module.Import(GetDbControllerDefinition(type).GetMethods().First(m => m.Name == "AfterTest"))));
         disposeMethod.Body.Instructions.InsertBefore(finallyEndInstruction, Instruction.Create(OpCodes.Endfinally));

         disposeMethod.Body.InitLocals = true;
         disposeMethod.Body.OptimizeMacros();
      }

      private TypeDefinition GetTestOutputHelperDefinition(TypeDefinition type)
      {
         TypeDefinition dbFixture = null;
         var assemblyRef =
            type.Module.AssemblyReferences.FirstOrDefault(ar => ar.Name == "Xunit.Abstractions");

         if (assemblyRef == null)
         {
            var assemblyPath =
               Path.Combine(Path.GetDirectoryName(ModuleDefinition.FullyQualifiedName), "Xunit.Abstractions.dll");

            if (File.Exists(assemblyPath))
            {
               dbFixture =
                  AssemblyDefinition.ReadAssembly(assemblyPath).MainModule.GetType("Xunit.Abstractions.ITestOutputHelper");
            }
            else
            {
               throw new WeavingException(
                  "A reference to XUnit.Abstractions was required but was not found. Please add the Nuget package XUnit.Abstractions (v2.0).");
            }
         }
         else
         {
            dbFixture = AssemblyResolver.Resolve(assemblyRef).MainModule.GetType("Xunit.Abstractions.ITestOutputHelper");
         }

         return dbFixture;
      }

      private TypeDefinition GetDatabasesFixtureDefinition(TypeDefinition type)
      {
         // Diagnostics.
         LogInfo("Assembly References detected:");
         type.Module.AssemblyReferences.ToList().ForEach(anr => LogInfo(anr.Name));

         TypeDefinition dbFixture = null;
         var DbTestMonkeyDatabasesAssemblyRef =
            type.Module.AssemblyReferences.FirstOrDefault(ar => ar.Name == "DbTestMonkey");

         if (DbTestMonkeyDatabasesAssemblyRef == null)
         {
            var assemblyPath =
               Path.Combine(Path.GetDirectoryName(ModuleDefinition.FullyQualifiedName), "DbTestMonkey.dll");

            if (File.Exists(assemblyPath))
            {
               dbFixture =
                  AssemblyDefinition.ReadAssembly(assemblyPath).MainModule.GetType("DbTestMonkey.DatabaseFixture");
            }
            else
            {
               throw new WeavingException(
                  "A reference to DbTestMonkey was required but was not found. Please add the Nuget package DbTestMonkey.");
            }
         }
         else
         {
            dbFixture = AssemblyResolver.Resolve(DbTestMonkeyDatabasesAssemblyRef).MainModule.GetType("DbTestMonkey.DatabaseFixture");
         }

         return dbFixture;
      }

      private TypeDefinition GetDbControllerDefinition(TypeDefinition type)
      {
         // Diagnostics.
         LogInfo("Assembly References detected:");
         type.Module.AssemblyReferences.ToList().ForEach(anr => LogInfo(anr.Name));

         TypeDefinition dbController = null;
         var DbTestMonkeyDatabasesAssemblyRef =
            type.Module.AssemblyReferences.FirstOrDefault(ar => ar.Name == "DbTestMonkey");

         if (DbTestMonkeyDatabasesAssemblyRef == null)
         {
            var assemblyPath =
               Path.Combine(Path.GetDirectoryName(ModuleDefinition.FullyQualifiedName), "DbTestMonkey.dll");

            if (File.Exists(assemblyPath))
            {
               dbController =
                  AssemblyDefinition.ReadAssembly(assemblyPath).MainModule.GetType("DbTestMonkey.DbController");
            }
            else
            {
               throw new WeavingException(
                  "A reference to DbTestMonkey was required but was not found. Please add the Nuget package DbTestMonkey.");
            }
         }
         else
         {
            dbController = AssemblyResolver.Resolve(DbTestMonkeyDatabasesAssemblyRef).MainModule.GetType("DbTestMonkey.DbController");
         }

         return dbController;
      }
   }
}