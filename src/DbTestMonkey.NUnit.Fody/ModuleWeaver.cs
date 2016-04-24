namespace DbTestMonkey.NUnit.Fody
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
   using global::NUnit.Framework;

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

            foreach (var type in matchingTypes)
            {
               // Ensure required decorated methods exists.
               EnsureMethodWithAttributeExistsInType(type, typeof(SetUpAttribute));
               EnsureMethodWithAttributeExistsInType(type, typeof(TearDownAttribute));
               EnsureMethodWithAttributeExistsInType(type, typeof(OneTimeSetUpAttribute));
               EnsureMethodWithAttributeExistsInType(type, typeof(OneTimeTearDownAttribute));

               // Per-class method weaving.
               WeavePerClassSetUp(type);
               LogInfo(type.Name + " has had database setup code injected into the set up method.");

               WeavePerClassTearDown(type);
               LogInfo(type.Name + " has had database shutdown code injected into the tear down method.");

               // Per-test method weaving.
               WeavePerTestSetUp(type);
               LogInfo(type.Name + " has had database setup code injected into the set up method.");

               WeavePerTestTearDown(type);
               LogInfo(type.Name + " has had database shutdown code injected into the tear down method.");
            }
         }
         catch (Exception ex)
         {
            LogError(ex.Message);
         }
      }

      /// <summary>
      /// Ensures that the specified type has a method with decorating attribute.
      /// A method is created to fulfill the criteria if not.
      /// </summary>
      /// <param name="hostType">The type to ensure the method exists in.</param>
      /// <param name="attributeType">The type of attribute to check for and create if need be.</param>
      private void EnsureMethodWithAttributeExistsInType(TypeDefinition hostType, Type attributeType)
      {
         if (hostType.Methods.Any(m => m.CustomAttributes.Any(a => a.AttributeType.Name == attributeType.Name)))
         {
            LogInfo(hostType.Name + " already has a " + attributeType.Name + " decorated method.");
         }
         else
         {
            LogInfo(hostType.Name + " does not yet have a " + attributeType.Name + " decorated method. Injecting a method with the decoration.");

            WeaveAttributeDecoratedMethod(hostType, attributeType);

            LogInfo(hostType.Name + " now has a method with " + attributeType.Name + " attribute.");
         }
      }

      /// <summary>
      /// Weaves a new method into a hosting type with an attribute of specified type being
      /// installed as a method decorator.
      /// </summary>
      /// <param name="hostType">The type of object to weave the new method into.</param>
      /// <param name="attributeType">The type of attribute to decorate the method with.</param>
      private void WeaveAttributeDecoratedMethod(TypeDefinition hostType, Type attributeType)
      {
         var disposeMethodAttribs =
            Mono.Cecil.MethodAttributes.Public |
            Mono.Cecil.MethodAttributes.Final |
            Mono.Cecil.MethodAttributes.HideBySig |
            Mono.Cecil.MethodAttributes.NewSlot |
            Mono.Cecil.MethodAttributes.Virtual;

         var setUpMethodDefinition =
            new MethodDefinition("Db" + Guid.NewGuid().ToString().Replace("-", ""), disposeMethodAttribs, ModuleDefinition.TypeSystem.Void);

         // Empty method shell that only returns void.
         setUpMethodDefinition.Body.Instructions.Add(Instruction.Create(OpCodes.Nop));
         setUpMethodDefinition.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));

         // First we need to get the constructor of the attribute.
         MethodReference attributeConstructor = ModuleDefinition.ImportReference(
            attributeType.GetConstructor(new Type[] { }));

         // Once we have the constructor, the custom attribute can be assembled and added.
         CustomAttribute setUpAttribute = new CustomAttribute(attributeConstructor);

         setUpMethodDefinition.CustomAttributes.Add(setUpAttribute);

         hostType.Methods.Add(setUpMethodDefinition);
      }

      /// <summary>
      /// Injects code for performing initial set up of the database.
      /// </summary>
      /// <param name="type">The type of object which is currently the weaving target.</param>
      private void WeavePerClassSetUp(TypeDefinition type)
      {
         LogInfo("Beginning to inject database set up into the one time set up method of type " + type.Name);

         MethodDefinition methodDefinition = type.GetOneTimeSetUpMethod();

         var firstInstruction = methodDefinition.Body.Instructions.First();

         LogInfo("About to inject BeforeTestGroup logic.");

         /*
          * Below instructions create the following line of code:
          *    Environment.CurrentDirectory = TextContext.CurrentContext.TestDirectory;
          * */
         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Call,
               methodDefinition.Module.ImportReference(
                  GetTestContextDefinition().Methods.First(m => m.Name == "get_CurrentContext"))));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Callvirt,
               methodDefinition.Module.ImportReference(
                  GetTestContextDefinition().Methods.First(m => m.Name == "get_TestDirectory"))));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Call,
               methodDefinition.Module.ImportReference(
                  GetEnvironmentDefinition().Methods.First(m => m.Name == "set_CurrentDirectory"))));

         /*
          * Below instructions create the following line of code:
          *    DbController.BeforeTestGroup(this.GetType(), new Action<string>(TestContext.WriteLine));
          * */

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(OpCodes.Ldarg_0));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Call,
               methodDefinition.Module.ImportReference(
                  typeof(Type).GetMethod("GetType", new Type[] { }))));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(OpCodes.Ldnull));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Ldftn,
               methodDefinition.Module.ImportReference(
                  GetTestContextDefinition().Methods.First(m =>
                     m.Name == "WriteLine" &&
                     m.Parameters.Count == 1 &&
                     m.Parameters.Single().ParameterType.FullName == "System.String"))));

         // Wrap a new action delegate around the logger delegate call.
         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Newobj,
               methodDefinition.Module.ImportReference(typeof(Action<string>).GetConstructors().First())));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Call,
               methodDefinition.Module.ImportReference(
                  GetDbControllerDefinition().GetMethods().First(m => m.Name == "BeforeTestGroup"))));

         LogInfo("BeforeTestGroup logic has been injected.");
      }

      /// <summary>
      /// Injects code for initialising database state and connections prior to test execution.
      /// </summary>
      /// <param name="type">The type of object which is currently the weaving target.</param>
      private void WeavePerTestSetUp(TypeDefinition type)
      {
         LogInfo("Beginning to inject database set up into the setup method of type " + type.Name);

         MethodDefinition methodDefinition = type.GetSetUpMethod();

         var firstInstruction = methodDefinition.Body.Instructions.First();

         LogInfo("About to inject BeforeTest logic.");

         /*
          * Below instructions create the following line of code:
          *    DbController.BeforeTest(this, MethodBase.GetCurrentMethod(), new Action<string>(TestContext.WriteLine));
          * */

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(OpCodes.Ldarg_0));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Call,
               methodDefinition.Module.ImportReference(typeof(MethodBase).GetMethod("GetCurrentMethod"))));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(OpCodes.Ldnull));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Ldftn,
               methodDefinition.Module.ImportReference(
                  GetTestContextDefinition().Methods.First(m => 
                     m.Name == "WriteLine" && 
                     m.Parameters.Count == 1 && 
                     m.Parameters.Single().ParameterType.FullName == "System.String"))));

         // Wrap a new action delegate around the logger delegate call.
         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Newobj,
               methodDefinition.Module.ImportReference(typeof(Action<string>).GetConstructors().First())));

         methodDefinition.Body.Instructions.InsertBefore(
            firstInstruction,
            Instruction.Create(
               OpCodes.Call,
               methodDefinition.Module.ImportReference(
                  GetDbControllerDefinition().GetMethods().First(m => m.Name == "BeforeTest"))));
      }

      /// <summary>
      /// Wraps the contents of the OneTimeTearDown method in a try/finally block where
      /// the AfterTestGroup method is called inside the finally.
      /// </summary>
      /// <param name="type">The type currently being weaved.</param>
      private void WeavePerClassTearDown(TypeDefinition type)
      {
         MethodDefinition tearDownMethod = type.GetOneTimeTearDownMethod();

         // Need to ensure we have markers throughout the method so that try/finally logic
         // can be inserted at the right places.
         var instructions = tearDownMethod.Body.Instructions;

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
          *       // Existing one time tear down code in here.
          *    }
          *    finally
          *    {
          *       DbController.AfterTestGroup(this.GetType(), new Action<string>(TestContext.WriteLine));
          *    }
          * */
         tearDownMethod.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
         {
            TryStart = startTryInstruction,
            TryEnd = finallyStartInstruction,
            HandlerStart = finallyStartInstruction,
            HandlerEnd = finallyEndInstruction
         });

         // Leaves the try block so execution enters the finally block.
         // Exits a protected region of code, unconditionally transferring control to a target instruction (short form).
         tearDownMethod.Body.Instructions.InsertBefore(
            finallyStartInstruction,
            Instruction.Create(OpCodes.Leave_S, finallyEndInstruction));

         /*
          * Below instructions create the following line of code:
          *    DbController.AfterTestGroup(this, new Action<string>(TestContext.WriteLine));
          */

         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction,
            Instruction.Create(OpCodes.Ldarg_0));

         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction,
            Instruction.Create(
               OpCodes.Call,
               tearDownMethod.Module.ImportReference(
                  typeof(Type).GetMethod("GetType", new Type[] { }))));

         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction,
            Instruction.Create(OpCodes.Ldnull));

         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction,
            Instruction.Create(
               OpCodes.Ldftn,
               tearDownMethod.Module.ImportReference(
                  GetTestContextDefinition().Methods.First(m =>
                     m.Name == "WriteLine" &&
                     m.Parameters.Count == 1 &&
                     m.Parameters.Single().ParameterType.FullName == "System.String"))));

         // Wrap a new action delegate around the logger delegate call.
         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction,
            Instruction.Create(
               OpCodes.Newobj,
               tearDownMethod.Module.ImportReference(typeof(Action<string>).GetConstructors().First())));

         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction,
            Instruction.Create(
               OpCodes.Call,
               tearDownMethod.Module.ImportReference(
                  GetDbControllerDefinition().GetMethods().First(m => m.Name == "AfterTestGroup"))));

         // Transfers control from the fault or finally clause of an exception block back to the 
         // Common Language Infrastructure (CLI) exception handler.
         tearDownMethod.Body.Instructions.InsertBefore(finallyEndInstruction, Instruction.Create(OpCodes.Endfinally));

         tearDownMethod.Body.InitLocals = true;
         tearDownMethod.Body.OptimizeMacros();
      }

      /// <summary>
      /// Wraps the contents of the TearDown method in a try/finally block where
      /// the AfterTest method is called inside the finally.
      /// </summary>
      /// <param name="type">The type currently being weaved.</param>
      private void WeavePerTestTearDown(TypeDefinition type)
      {
         MethodDefinition tearDownMethod = type.GetTearDownMethod();

         // Need to ensure we have markers throughout the method so that try/finally logic
         // can be inserted at the right places.
         var instructions = tearDownMethod.Body.Instructions;

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
          *       // Existing tear down code in here.
          *    }
          *    finally
          *    {
          *       DbController.AfterTest(MethodBase.GetCurrentMethod());
          *    }
          * */
         tearDownMethod.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Finally)
         {
            TryStart = startTryInstruction,
            TryEnd = finallyStartInstruction,
            HandlerStart = finallyStartInstruction,
            HandlerEnd = finallyEndInstruction
         });

         // Leaves the try block so execution enters the finally block.
         // Exits a protected region of code, unconditionally transferring control to a target instruction (short form).
         tearDownMethod.Body.Instructions.InsertBefore(
            finallyStartInstruction, 
            Instruction.Create(OpCodes.Leave_S, finallyEndInstruction));

         /*
          * Below instructions create the following line of code:
          *    DbController.AfterTest(MethodBase.GetCurrentMethod());
          */

         // Calls MethodBase.GetCurrentMethod() and places the result on the stack.
         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction, 
            Instruction.Create(
               OpCodes.Call, 
               tearDownMethod.Module.ImportReference(typeof(MethodBase).GetMethod("GetCurrentMethod"))));

         // Calls DbController.AfterTest(methodBase) and passes the value on the stack as the argument.
         tearDownMethod.Body.Instructions.InsertBefore(
            finallyEndInstruction, 
            Instruction.Create(
               OpCodes.Call, 
               tearDownMethod.Module.ImportReference(GetDbControllerDefinition().GetMethods().First(m => m.Name == "AfterTest"))));

         // Transfers control from the fault or finally clause of an exception block back to the 
         // Common Language Infrastructure (CLI) exception handler.
         tearDownMethod.Body.Instructions.InsertBefore(finallyEndInstruction, Instruction.Create(OpCodes.Endfinally));

         tearDownMethod.Body.InitLocals = true;
         tearDownMethod.Body.OptimizeMacros();
      }

      /// <summary>
      /// Scans the System assembly for the <see cref="Environment"/> type and returns the type definition.
      /// </summary>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="Environment"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if no reference to the System assembly was found and the binary itself could not be found either.</exception>
      private TypeDefinition GetEnvironmentDefinition()
      {
         return GetTypeDefinition(
            "mscorlib",
            "mscorlib.dll",
            "System.Environment");
      }

      /// <summary>
      /// Scans the NUnit.Framework assembly for the <see cref="TestContext"/> interface and returns the type definition.
      /// </summary>
      /// <returns>A <see cref="TypeDefinition"/> representing the <see cref="TestContext"/> class.</returns>
      /// <exception cref="WeavingException">
      /// Thrown if no reference to the NUnit.Framework assembly was found and the binary itself could not be found either.</exception>
      private TypeDefinition GetTestContextDefinition()
      {
         return GetTypeDefinition(
            "NUnit.Framework",
            "NUnit.Framework.dll",
            "NUnit.Framework.TestContext");
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