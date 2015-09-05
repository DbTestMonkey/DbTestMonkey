namespace DbTestMonkey.XUnit.Fody
{
   using System;
   using System.Collections.Generic;
   using System.Globalization;
   using System.IO;
   using System.Linq;
   using System.Reflection;
   using Mono.Cecil;
   using Mono.Cecil.Cil;
   using Mono.Cecil.Rocks;
   using Xunit.Abstractions;

   /// <summary>
   /// Fody class used for defining the weaving procedure for target assemblies.
   /// <a href="https://msdn.microsoft.com/en-us/library/System.Reflection.Emit.OpCodes(v=vs.110).aspx">
   /// OpCodes Reference</a>
   /// </summary>
   public class ModuleWeaver
   {
      /// <summary>
      /// Holds a string representation of a <see cref="Guid"/> that can be used to ensure weaved collections
      /// have unique names and will not clash with user collections.
      /// </summary>
      private string _guidCollectionName;

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
         LogInfo("Starting to weave module.");

         // Generate a new Guid collection name for this module.
         _guidCollectionName = Guid.NewGuid().ToString();

         try
         {
            IEnumerable<TypeDefinition> types = ModuleDefinition.GetTypes();
            
            // Weave any classes that have a UsesDatabasesAttribute on it or one or more properties have a ConnectionAttribute
            // attribute on them.
            var matchingTypes = types.Where(t => 
               t.CustomAttributes.Any(a => a.AttributeType.Name == "UsesDatabasesAttribute") || 
               t.Properties.Any(p => p.CustomAttributes.Any(pa => pa.AttributeType.Name == "ConnectionAttribute")));

            if (matchingTypes.Any())
            {
               LogInfo("One or more eligible types were found. Creating DbTestMonkeyXUnitRuntimeCollectionDefinition class.");

               var typeDefinition = new TypeDefinition(
                  "DbTestMonkey.Runtime",
                  "DbTestMonkeyXUnitRuntimeCollectionDefinition",
                  Mono.Cecil.TypeAttributes.Class | Mono.Cecil.TypeAttributes.Public);

               LogInfo("Class has been created. Preparing to attach CollectionAttribute.");

               // First we need to get the constructor of the attribute.
               MethodReference attributeConstructor = ModuleDefinition.ImportReference(
                  typeof(Xunit.CollectionDefinitionAttribute).GetConstructor(new Type[] { typeof(string) }));

               // Once we have the constructor, the custom attribute can be assembled and added.
               CustomAttribute collectionAttribute = new CustomAttribute(attributeConstructor);
               collectionAttribute.ConstructorArguments.Add(
                  new CustomAttributeArgument(ModuleDefinition.ImportReference(typeof(string)), _guidCollectionName));

               typeDefinition.CustomAttributes.Add(collectionAttribute);

               LogInfo(
                  "CollectionAttribute has been attached. Preparing to attach ICollectionFixture<CollectionDatabaseFixture> interface.");

               // Now ensure the class implements the ICollectionFixture<CollectionDatabaseFixture> interface.
               TypeDefinition dbFixture = GetCollectionDatabaseFixtureDefinition();

               var collectionFixture = GetXunitCollectionFixtureDefinition();

               var genericClassFixture =
                  collectionFixture.MakeGenericInstanceType(ModuleDefinition.ImportReference(dbFixture));

               typeDefinition.Interfaces.Add(ModuleDefinition.ImportReference(genericClassFixture));

               LogInfo("Interface has been added successfully.");
            }

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
         LogInfo("Beginning to inject database set up into the constructor of type " + type.Name);

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

            LogInfo("Ensuring that the constructor has an ITestOutputHelper dependency.");

            // Ensure the class constructor has a ITestHelperOutput parameter.
            if (!ctor.Parameters.Any(p => p.ParameterType.FullName == typeof(ITestOutputHelper).FullName))
            {
               ctor.Parameters.Add(
                  new ParameterDefinition(
                     type.Module.ImportReference(GetTestOutputHelperDefinition())));
            }

            LogInfo("Preparing to wire up ClassDatabaseFixture dependency.");

            // Wire up ClassDatabaseFixture dependency.
            EnsureClassHasClassDatabaseFixtureDependency(type, ctor, firstInstructionAfterBaseCtorCall);

            LogInfo("Preparing to wire up CollectionDatabaseFixture dependency.");

            // Wire up CollectionDatabaseFixture dependency.
            EnsureClassHasCollectionDatabaseFixtureDependency(type, ctor, firstInstructionAfterBaseCtorCall);

            LogInfo("About to inject BeforeTest logic.");

            /*
             * Below instructions create the following line of code:
             *    DbController.BeforeTest(this, MethodBase.GetCurrentMethod(), new Action<string>(testOutputHelper.WriteLine));
             * */

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(OpCodes.Ldarg_0));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Call,
                  ctor.Module.ImportReference(typeof(MethodBase).GetMethod("GetCurrentMethod"))));

            // Load the ITestOutputHelper instance onto the stack.
            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Ldarg_S,
                  ctor.Parameters.First(p => p.ParameterType.FullName == typeof(ITestOutputHelper).FullName)));

            // Duplicate the instruction on top of the evaluation stack.
            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(OpCodes.Dup));

            // Load the virtual function ITestOutputHelper.WriteLine onto the evaluation stack.
            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Ldvirtftn,
                  ctor.Module.ImportReference(
                     GetTestOutputHelperDefinition().Methods.First(m => m.Name == "WriteLine" && m.Parameters.Count == 1))));

            // Wrap a new action delegate around the ITestOutputHelper.WriteLine delegate call.
            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Newobj,
                  ctor.Module.ImportReference(typeof(Action<string>).GetConstructors().First())));

            ctor.Body.Instructions.InsertBefore(
               firstInstructionAfterBaseCtorCall,
               Instruction.Create(
                  OpCodes.Call,
                  ctor.Module.ImportReference(
                     GetDbControllerDefinition().GetMethods().First(m => m.Name == "BeforeTest"))));
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
            type.Interfaces.Add(type.Module.ImportReference(typeof(IDisposable)));

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
      /// Checks whether the target class has a dependency to the <see cref="ClassDatabaseFixture"/> class
      /// and takes it in the constructor as well.
      /// </summary>
      /// <param name="type">The target type currently being weaved.</param>
      /// <param name="ctor">A <see cref="MethodDefinition"/> representing the constructor being weaved.</param>
      /// <param name="firstInstructionAfterBaseCtorCall">The first IL instruction after the call to base.ctor().</param>
      private void EnsureClassHasClassDatabaseFixtureDependency(
         TypeDefinition type, 
         MethodDefinition ctor, 
         Instruction firstInstructionAfterBaseCtorCall)
      {
         // Ensure class implements IClassFixture<ClassDatabaseFixture>
         if (!type.Interfaces.Any(t =>
               t.HasGenericParameters && t.GenericParameters.Any(p =>
                  p.FullName == typeof(ClassDatabaseFixture).FullName)))
         {
            // Resolve ClassDatabaseFixture reference.
            TypeDefinition dbFixture = GetClassDatabaseFixtureDefinition();

            var genericClassFixture =
               GetXunitClassFixtureDefinition().MakeGenericInstanceType(type.Module.ImportReference(dbFixture));

            type.Interfaces.Add(type.Module.ImportReference(genericClassFixture));
         }

         EnsureClassHasFixtureDependency(
            type,
            typeof(ClassDatabaseFixture),
            ctor,
            firstInstructionAfterBaseCtorCall,
            GetClassDatabaseFixtureDefinition);
      }

      /// <summary>
      /// Checks whether the target class has a dependency to the <see cref="CollectionDatabaseFixture"/> class
      /// and takes it in the constructor as well.
      /// </summary>
      /// <param name="type">The target type currently being weaved.</param>
      /// <param name="ctor">A <see cref="MethodDefinition"/> representing the constructor being weaved.</param>
      /// <param name="firstInstructionAfterBaseCtorCall">The first IL instruction after the call to base.ctor().</param>
      private void EnsureClassHasCollectionDatabaseFixtureDependency(
         TypeDefinition type,
         MethodDefinition ctor,
         Instruction firstInstructionAfterBaseCtorCall)
      {
         // Ensure class has a collection attribute.
         if (!type.CustomAttributes.Any(a =>
               a.AttributeType.FullName == typeof(Xunit.CollectionAttribute).FullName))
         {
            LogInfo("Importing reference to CollectionAttribute class.");

            // First we need to get the constructor of the attribute.
            MethodReference attributeConstructor = ModuleDefinition.ImportReference(
               typeof(Xunit.CollectionAttribute).GetConstructor(new Type[] { typeof(string) }));

            LogInfo("Assembling CollectionAttribute in preparation for attaching it to the class.");

            // Once we have the constructor, the custom attribute can be assembled and added.
            CustomAttribute collectionAttribute = new CustomAttribute(attributeConstructor);
            collectionAttribute.ConstructorArguments.Add(
               new CustomAttributeArgument(ModuleDefinition.ImportReference(typeof(string)), _guidCollectionName));

            LogInfo("Attaching the custom attribute to the class.");

            type.CustomAttributes.Add(collectionAttribute);

            LogInfo("Custom attribute has been attached.");
         }
         else
         {
            // Attribute can only exist once on a class and it can only accept one argument. 
            // This is safe to check like this.
            var collectionName = type.CustomAttributes.First(a =>
               a.AttributeType.FullName == typeof(Xunit.CollectionAttribute).FullName).ConstructorArguments.First().Value;

            LogInfo(
               "Type " + type.Name + 
               " belongs to collection " + collectionName + 
               ". Attempting to augment the collection definition class with DbTestMonkey logic.");

            
            var types = ModuleDefinition.Types.Where(t => 
               t.CustomAttributes.Any(a => 
                  a.AttributeType.FullName == typeof(Xunit.CollectionDefinitionAttribute).FullName && 
                  a.ConstructorArguments.First().Value.ToString() == collectionName.ToString()));

            if (!types.Any())
            {
               throw new WeavingException(
                  "The collection definition class for the collection '" + collectionName.ToString() + "' could not be found. " +
                  "Collection definitions outside the target assembly are not currently supported by DbTestMonkey.");
            }
            else
            {
               var definitionType = types.First();

               // Just make sure the user hasn't somehow added the DbTestMonkey fixture already. Don't double add it.
               if (!definitionType.Interfaces.Any(i => i.FullName == typeof(Xunit.ICollectionFixture<CollectionDatabaseFixture>).FullName))
               {
                  TypeDefinition dbFixture = GetCollectionDatabaseFixtureDefinition();

                  var genericClassFixture =
                     GetXunitCollectionFixtureDefinition()
                        .MakeGenericInstanceType(ModuleDefinition.ImportReference(dbFixture));

                  definitionType.Interfaces.Add(ModuleDefinition.ImportReference(genericClassFixture));
               }
            }
         }

         EnsureClassHasFixtureDependency(
            type,
            typeof(CollectionDatabaseFixture),
            ctor,
            firstInstructionAfterBaseCtorCall,
            GetCollectionDatabaseFixtureDefinition);
      }

      /// <summary>
      /// Ensures that the target type has the required fixture dependency.
      /// </summary>
      /// <param name="typeDef">The type definition representing the weaving target.</param>
      /// <param name="type">The type of the fixture that should be depended on.</param>
      /// <param name="ctor">The constructor of the type currently being weaved.</param>
      /// <param name="firstInstructionAfterBaseCtorCall">
      /// The instruction before which all weaved code must exist.</param>
      /// <param name="fixtureDefinitionDelegate">
      /// A delegate used to produce the type definition which this class must depend on.</param>
      private void EnsureClassHasFixtureDependency(
         TypeDefinition typeDef,
         Type type,
         MethodDefinition ctor,
         Instruction firstInstructionAfterBaseCtorCall,
         Func<TypeDefinition> fixtureDefinitionDelegate)
      {
         if (!ctor.Parameters.Any(p => p.ParameterType.FullName == type.FullName))
         {
            ctor.Parameters.Add(
               new ParameterDefinition(
                  ModuleDefinition.ImportReference(fixtureDefinitionDelegate())));
         }

         /*
          * Below instructions create the following line of code:
          *    dbFixture.LogAction = new Action<string>(testOutputHelper.WriteLine);
          * */

         // Loads the value of the xDatabaseFixture argument onto the stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Ldarg_S,
               ctor.Parameters.First(p => p.ParameterType.FullName == type.FullName)));

         // Loads the value of the ITestOutputHelper argument onto the stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Ldarg_S,
               ctor.Parameters.First(p => p.ParameterType.FullName == typeof(ITestOutputHelper).FullName)));

         // Duplicates the ITestOutputHelper instruction on the stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(OpCodes.Dup));

         // Creates a reference to the virtual function testOutputHelper.WriteLine and loads it onto the stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Ldvirtftn,
               ctor.Module.ImportReference(
                  GetTestOutputHelperDefinition().Methods.First(m => m.Name == "WriteLine" && m.Parameters.Count == 1))));

         // Creates a new Action<string> object instance using the previous virtual function as a constructor argument
         // and places it onto the stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Newobj,
               ctor.Module.ImportReference(typeof(Action<string>).GetConstructors().First())));

         // Calls dbFixture.LogAction = logAction, where logAction is the newly created Action<string> object.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Callvirt,
               ctor.Module.ImportReference(
                  fixtureDefinitionDelegate().GetMethods().First(m => m.Name == "set_LogAction"))));

         /*
          * Below instructions create the following line of code:
          *    dbFixture.ClassType = base.GetType();
          * */

         // Loads the value of the xDatabaseFixture argument onto the stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Ldarg_S,
               ctor.Parameters.First(p => p.ParameterType.FullName == type.FullName)));

         // Converts the metadata type token into its runtime representation and 
         // pushes it onto the evaluation stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(OpCodes.Ldtoken, typeDef));

         // Calls base.GetType() and loads the result onto the stack.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Call,
               ctor.Module.ImportReference(typeof(Type).GetMethod("GetTypeFromHandle"))));

         // Calls dbFixture.ClassType = type, where type is the result of the previous instruction.
         ctor.Body.Instructions.InsertBefore(
            firstInstructionAfterBaseCtorCall,
            Instruction.Create(
               OpCodes.Callvirt,
               ctor.Module.ImportReference(
                  fixtureDefinitionDelegate().GetMethods().First(m => m.Name == "set_ClassType"))));
      }

      /// <summary>
      /// Wraps the contents of the Dispose method in a try/finally block where
      /// the AfterTest method is called inside the finally.
      /// </summary>
      /// <param name="type">The type currently being weaved.</param>
      private void InjectTryFinallyWithAfterTestCall(TypeDefinition type)
      {
         MethodDefinition disposeMethod = type.GetDisposeMethod();

         // Need to ensure we have markers throughout the method so that try/finally logic
         // can be inserted at the right places.
         var instructions = disposeMethod.Body.Instructions;

         var startTryInstruction = instructions.First();

         // Need to create a Nop instruction as you can't reference a gap between instructions.
         // Worst case this will consume a CPU cycle; not a major deal.
         var finallyStartInstruction = Instruction.Create(OpCodes.Nop);
         instructions.InsertBefore(instructions.Last(), finallyStartInstruction);

         var finallyEndInstruction = instructions.Last();

         /*
          * Below instructions create the following lines of code:
          *    try
          *    {
          *       // Existing dispose code in here.
          *    }
          *    finally
          *    {
          *       DbController.AfterTest(MethodBase.GetCurrentMethod());
          *    }
          * */
         disposeMethod.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
         {
            TryStart = startTryInstruction,
            TryEnd = finallyStartInstruction,
            HandlerStart = finallyStartInstruction,
            HandlerEnd = finallyEndInstruction
         });

         // Leaves the try block so execution enters the finally block.
         // Exits a protected region of code, unconditionally transferring control to a target instruction (short form).
         disposeMethod.Body.Instructions.InsertBefore(
            finallyStartInstruction, 
            Instruction.Create(OpCodes.Leave_S, finallyEndInstruction));

         /*
          * Below instructions create the following line of code:
          *    DbController.AfterTest(MethodBase.GetCurrentMethod());
          */

         // Calls MethodBase.GetCurrentMethod() and places the result on the stack.
         disposeMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction, 
            Instruction.Create(
               OpCodes.Call, 
               disposeMethod.Module.ImportReference(typeof(MethodBase).GetMethod("GetCurrentMethod"))));

         // Calls DbController.AfterTest(methodBase) and passes the value on the stack as the argument.
         disposeMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction, 
            Instruction.Create(
               OpCodes.Call, 
               disposeMethod.Module.ImportReference(GetDbControllerDefinition().GetMethods().First(m => m.Name == "AfterTest"))));

         // Transfers control from the fault or finally clause of an exception block back to the 
         // Common Language Infrastructure (CLI) exception handler.
         disposeMethod.Body.Instructions.InsertBefore(finallyEndInstruction, Instruction.Create(OpCodes.Endfinally));

         disposeMethod.Body.InitLocals = true;
         disposeMethod.Body.OptimizeMacros();
      }

      /// <summary>
      /// Scans the xunit.abstractions assembly for the <see cref="ITestOutputHelper"/> interface and returns the type definition.
      /// </summary>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="ITestOutputHelper"/> interface.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if no reference to the xunit.abstractions assembly was found and the binary itself could not be found either.</exception>
      private TypeDefinition GetTestOutputHelperDefinition()
      {
         return GetTypeDefinition(
            "xunit.abstractions",
            "Xunit.Abstractions.dll",
            "Xunit.Abstractions.ITestOutputHelper");
      }

      /// <summary>
      /// Scans the xunit.core assembly for the <see cref="Xunit.IClassFixture{TFixture}"/> interface and returns the type definition.
      /// </summary>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="Xunit.IClassFixture{TFixture}"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if a reference to the xunit.core assembly was unable to be established.</exception>
      private TypeDefinition GetXunitClassFixtureDefinition()
      {
         return GetTypeDefinition(
            "xunit.core",
            "xunit.core.dll",
            "Xunit.IClassFixture`1");
      }

      /// <summary>
      /// Scans the xunit.core assembly for the <see cref="Xunit.ICollectionFixture{TFixture}"/> interface and returns the type definition.
      /// </summary>   
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="Xunit.ICollectionFixture{TFixture}"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if a reference to the xunit.core assembly was unable to be established.</exception>
      private TypeDefinition GetXunitCollectionFixtureDefinition()
      {
         return GetTypeDefinition(
            "xunit.core",
            "xunit.core.dll",
            "Xunit.ICollectionFixture`1");
      }

      /// <summary>
      /// Scans the DbTestMonkey.XUnit.Fody assembly for the <see cref="ClassDatabaseFixture"/> class and returns the type definition.
      /// </summary>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="ClassDatabaseFixture"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if a reference to the DbTestMonkey.XUnit.Fody assembly was unable to be established.</exception>
      private TypeDefinition GetClassDatabaseFixtureDefinition()
      {
         return GetTypeDefinition(
            "DbTestMonkey.XUnit.Fody",
            "DbTestMonkey.XUnit.Fody.dll",
            "DbTestMonkey.XUnit.Fody.ClassDatabaseFixture");
      }

      /// <summary>
      /// Scans the DbTestMonkey.XUnit.Fody assembly for the <see cref="CollectionDatabaseFixture"/> class and returns the type definition.
      /// </summary>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="CollectionDatabaseFixture"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if a reference to the DbTestMonkey.XUnit.Fody assembly was unable to be established.</exception>
      private TypeDefinition GetCollectionDatabaseFixtureDefinition()
      {
         return GetTypeDefinition(
            "DbTestMonkey.XUnit.Fody", 
            "DbTestMonkey.XUnit.Fody.dll", 
            "DbTestMonkey.XUnit.Fody.CollectionDatabaseFixture");
      }

      /// <summary>
      /// Scans the DbTestMonkey assembly for the <see cref="DbController"/> static class and returns the type definition.
      /// </summary>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="DbController"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if no reference to the DbTestMonkey assembly was found and the binary itself could not be found either.</exception>
      private TypeDefinition GetDbControllerDefinition()
      {
         return GetTypeDefinition("DbTestMonkey", "DbTestMonkey.dll", "DbTestMonkey.DbController");
      }

      /// <summary>
      /// Scans assembly references for a specific type and returns the <see cref="TypeDefinition"/>.
      /// </summary>
      /// <param name="assemblyName">The name of the assembly the type should be found in.</param>
      /// <param name="fileName">The name of the binary which contains the assembly. 
      /// Used as a fallback if the dependency has been optimised away.</param>
      /// <param name="fullTypeName">The full name of the type that is being searched for.</param>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="DbController"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if no reference to the DbTestMonkey assembly was found and the binary itself could not be found either.</exception>
      private TypeDefinition GetTypeDefinition(string assemblyName, string fileName, string fullTypeName)
      {
         TypeDefinition typeDef = null;
         var assemblyRef =
            ModuleDefinition.AssemblyReferences.FirstOrDefault(ar => ar.Name == assemblyName);

         // If the assembly reference could not be found because it has been optimised away, check the output directory
         // for the binary itself.
         if (assemblyRef == null)
         {
            var assemblyPath =
               Path.Combine(Path.GetDirectoryName(ModuleDefinition.FullyQualifiedName), fileName);

            if (File.Exists(assemblyPath))
            {
               typeDef =
                  AssemblyDefinition.ReadAssembly(assemblyPath).MainModule.GetType(fullTypeName);
            }
            else
            {
               throw new WeavingException(
                  string.Format(
                     CultureInfo.InvariantCulture, 
                     "A reference to {0} was required but was not found. Please add the Nuget package {0}.", 
                     assemblyName));
            }
         }
         else
         {
            typeDef = AssemblyResolver.Resolve(assemblyRef).MainModule.GetType(fullTypeName);
         }

         return typeDef;
      }
   }
}