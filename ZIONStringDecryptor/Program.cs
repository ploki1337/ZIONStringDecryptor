using System.Collections;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

class Program
{
    public static Hashtable hTable = new Hashtable();
    static int decryptedStrings = 0;

    static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.InputEncoding = System.Text.Encoding.UTF8;
        Console.Title = "https://github.com/ploki1337";

        if (args.Length != 0)
        {
            string assemblyPath = args[0];
            var module = ModuleDefMD.Load(assemblyPath);
            try
            {
                var moduleType = module.Types.FirstOrDefault(t => t.Name == "<Module>");

                if (moduleType != null)
                {
                    var hTableField = moduleType.Fields.FirstOrDefault(f => f.Name == "hTable");

                    if (hTableField != null)
                    {
                        HashSet<MethodDef> methodsUsingHTable = new();
                        List<TypeDef> typesToRemove = new();
                        

                        foreach (var type in module.Types)
                        {
                            foreach (var method in type.Methods)
                            {
                                if (method.HasBody)
                                {
                                    bool usesHTable = method.Body.Instructions.Any(instr =>
                                        instr.Operand is IField field && field.FullName == hTableField.FullName);

                                    if (usesHTable)
                                    {
                                        methodsUsingHTable.Add(method);
                                        if (!typesToRemove.Contains(type))
                                        {
                                            typesToRemove.Add(type);
                                        }
                                    }
                                }
                            }
                        }

                        foreach (var type in module.Types)
                        {
                            foreach (var method in type.Methods)
                            {
                                if (method.HasBody)
                                {
                                    decryptedStrings += ProcessMethodInstructions(method, methodsUsingHTable);
                                }
                            }
                        }

                        moduleType.Fields.Remove(hTableField);

                        foreach (var typeToRemove in typesToRemove)
                        {
                            module.Types.Remove(typeToRemove);
                        }

                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"[+] Successfully decrypted {decryptedStrings} strings!");
                    }
                }
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[!] Error: {e.Message}");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("[i] Press any key to exit");
                Console.ReadKey();
            }
            finally
            {
                try
                {
                    if (decryptedStrings > 0)
                    {
                        string outputPath = Path.GetFileNameWithoutExtension(assemblyPath) + "-decrypted.exe";
                        var options = new ModuleWriterOptions(module)
                        {
                            MetadataOptions = { Flags = MetadataFlags.KeepOldMaxStack }
                        };
                        module.Write(outputPath, options);
                        Console.ForegroundColor = ConsoleColor.Gray;
                        Console.WriteLine($"[/] Saved at {outputPath}!");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("[i] Press any key to exit");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"[!] Encrypted strings not found!");
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine("[i] Press any key to exit");
                        Console.ReadKey();
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[!] Error: {e.Message}");
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine("[i] Press any key to exit");
                    Console.ReadKey();
                }
            }
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[!] Unknown path.");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[i] Press any key to exit");
            Console.ReadKey();
        }
    }

    public static int ProcessMethodInstructions(MethodDef method, HashSet<MethodDef> methodsUsingHTable)
    {
        int decryptedStrings = 0;

        try
        {
            var instructionsToRemove = new List<Instruction>();

            for (int i = 0; i < method.Body.Instructions.Count; i++)
            {
                var instruction = method.Body.Instructions[i];

                if (instruction.OpCode == OpCodes.Ldstr)
                {
                    var str = (string)instruction.Operand;
                    var decryptedString = DecryptString(str);
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"[+] Found encrypted string: \"{str}\" in method: {method.Name}\n    Decrypted: \"{decryptedString}\"");

                    if (decryptedString != str)
                    {
                        decryptedStrings++;
                    }

                    instruction.Operand = decryptedString;
                }

                if ((instruction.OpCode == OpCodes.Call || instruction.OpCode == OpCodes.Callvirt) &&
                    instruction.Operand is IMethod calledMethod && methodsUsingHTable.Contains(calledMethod))
                {
                    var arguments = new List<string>();
                    var methodSig = calledMethod.MethodSig;

                    if (methodSig != null)
                    {
                        var argInstructions = method.Body.Instructions.Take(i).ToList();
                        var stack = new Stack<object>();

                        foreach (var argInstruction in argInstructions)
                        {
                            if (argInstruction.OpCode == OpCodes.Ldstr)
                            {
                                stack.Push(argInstruction.Operand);
                            }
                        }

                        while (stack.Count > 0)
                        {
                            var arg = stack.Pop();
                            if (arg is string str)
                            {
                                arguments.Add(str);
                            }
                        }

                        if (arguments.Count > 0)
                        {
                            if (i > 0 && method.Body.Instructions[i - 1].OpCode == OpCodes.Ldc_I8)
                            {
                                instructionsToRemove.Add(method.Body.Instructions[i - 1]);
                                instructionsToRemove.Add(instruction);
                            }
                        }
                    }
                }
            }

            foreach (var instruction in instructionsToRemove)
            {
                method.Body.Instructions.Remove(instruction);
            }

            method.Body.KeepOldMaxStack = true;
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[!] Error: {e.Message}");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("[i] Press any key to exit");
            Console.ReadKey();
        }

        return decryptedStrings;
    }

    public static string DecryptString(string str)
    {
        if (hTable.ContainsKey(str))
        {
            return hTable[str] as string ?? string.Empty;
        }

        char[] resultArray = str.ToCharArray();
        for (int i = 0; i < resultArray.Length; i++)
        {
            for (int j = 0; j < 1111; j++)
            {
                resultArray[i] ^= (char)1;
            }
        }

        string result = new string(resultArray);
        hTable[str] = result;
        return result;
    }
}
